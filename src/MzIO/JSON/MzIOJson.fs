﻿namespace MzIO.Json


open System
open System.IO
open System.Globalization
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open Newtonsoft.Json.Serialization
open MzIO.IO
open MzIO.Model
open MzIO.Model.CvParam


type MzIOJson =

    static member jsonSettings = 
        let tmp = new JsonSerializerSettings()
        //new method to preserve paramcontainer fields when serealizing type
        tmp.ReferenceLoopHandling       <- Newtonsoft.Json.ReferenceLoopHandling.Serialize
        tmp.PreserveReferencesHandling  <- Newtonsoft.Json.PreserveReferencesHandling.Objects
        //end of new method
        //tmp.ReferenceLoopHandling       <- Newtonsoft.Json.ReferenceLoopHandling.Ignore
        tmp.ContractResolver            <- new DefaultContractResolver()
        tmp.Culture <- new CultureInfo("en-US")    
        tmp


    static member SaveJsonFile(obj:Object, path:string) =
        if File.Exists(path) then
            File.Delete(path)

        use writer      = File.CreateText(path)
        use jsonWriter  = new JsonTextWriter(writer)
        (
            let serializer = JsonSerializer.Create(MzIOJson.jsonSettings)
            serializer.Formatting <- Formatting.Indented
            serializer.Serialize(jsonWriter, obj)
        )

    static member ReadJsonFile<'T>(path:string) =
        if File.Exists(path) = false then 
            raise (new FileNotFoundException(path))

        use reader = File.OpenText(path)
        use jsonReader = new JsonTextReader(reader)
        (
            let serializer = JsonSerializer.Create(MzIOJson.jsonSettings)
            let tmp = serializer.Deserialize<'T>(jsonReader)
            tmp
        )


    static member HandleExternalModelFile(io:IMzIOIO, path:string) =
        
        //to be safe it is false
        let mutable throwExceptionIfFileCouldNotRead = false

        if String.IsNullOrWhiteSpace(path) then 
            raise (ArgumentNullException("path"))
        else
            ()

        if not (File.Exists(path)) then
            let model = io.CreateDefaultModel()
            MzIOJson.SaveJsonFile(model, path)
            model
        
        else
            try
                MzIOJson.ReadJsonFile<MzIOModel>(path)
            with
                | :? Exception as ex
                    -> 
                        match throwExceptionIfFileCouldNotRead with
                        | true   -> raise ex
                        | false  ->
                            let mutable causalException = ex.InnerException
                            //let mutable backFile = String.Format("{0}.back", path)
                            let backFile = path + ".back"

                            if File.Exists(backFile) then
                                File.Delete(backFile)
                            File.Move(path, backFile)

                            //model <- io.CreateDefaultModel()
                            let model = io.CreateDefaultModel()
                            MzIOJson.SaveJsonFile(model, path)

                            let mutable msg = System.Text.StringBuilder()
                            //msg.AppendFormat
                            //    ("Could not read mz lite model file: '{0}'. ", 
                            //        path
                            //    ) |> ignore
                            //msg.AppendFormat
                            //    ("Causal exception is: '{0}' with message '{1}'. ", 
                            //        causalException.GetType().FullName, causalException.Message
                            //    ) |> ignore
                            //msg.AppendFormat
                            //    ("A new initial model file was created, the old file 
                            //        was renamed to: '{0}'.", backFile
                            //    ) |> ignore
                            Console.Error.WriteLine(msg.ToString())
                            model

    static member FromJson<'T>(json: string) =
        if String.IsNullOrWhiteSpace(json) then 
            raise (ArgumentNullException("json"))
        else
            (*JsonConvert.DeserializeObject<'T>(json)*)
            let tmp = JsonConvert.DeserializeObject<'T>(json)
            match tmp :> Object with
            | :? DynamicObj as item -> MzIOJson.deserializeJObject(item, tmp :> Object)
            | _                     -> ()
            tmp

    static member deserializeJObject(baseObj:DynamicObj, jsonObj:Object) =
        match jsonObj with
        | :? DynamicObj as value ->
            value.GetProperties true
            |> Seq.iter (fun item -> MzIOJson.deserializeJObject(value, item.Value))
        | _ -> 
            if (jsonObj :? JObject) = true then
                if
                    (jsonObj :?> JObject).["CvAccession"] <> null && 
                    (jsonObj :?> JObject).["Type"] <> null then
                        let tmp = JsonConvert.DeserializeObject<CvParam<IConvertible>>(jsonObj.ToString())
                        baseObj.SetValue(tmp.CvAccession, tmp)
                else
                    if
                        (jsonObj :?> JObject).["Name"] <> null && 
                        (jsonObj :?> JObject).["Type"] <> null then
                            let tmp = JsonConvert.DeserializeObject<UserParam<IConvertible>>(jsonObj.ToString())
                            baseObj.SetValue(tmp.Name, tmp)
                    else 
                        let jString = jsonObj.ToString()
                        match baseObj.ToString() with
                        | "MzIO.Model.ScanList" -> 
                            let tmp = JsonConvert.DeserializeObject<Scan>(jString)
                            baseObj.SetValue(tmp.ToString(), tmp)
                            MzIOJson.deserializeJObject(tmp, tmp)
                        | "MzIO.Model.ProductList" -> 
                            let tmp = JsonConvert.DeserializeObject<Product>(jString)
                            baseObj.SetValue(tmp.ToString(), tmp)
                            MzIOJson.deserializeJObject(tmp, tmp)
                        | "MzIO.Model.PrecursorList" -> 
                            let tmp = JsonConvert.DeserializeObject<Precursor>(jString)
                            baseObj.SetValue(tmp.ToString(), tmp)
                            MzIOJson.deserializeJObject(tmp, tmp)
                        | _ -> ()
            else ()

    static member ToJson(obj:Object) =

        JsonConvert.SerializeObject(obj, MzIOJson.jsonSettings)
