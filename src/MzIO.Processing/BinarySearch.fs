﻿namespace MzIO.Processing

open System
open System.Collections.Generic
open System.Linq
open MzIO.Model
open MzIO.Commons.Arrays
open MzIO.Commons.Arrays.MzIOArray
open MzIO.MetaData.PSIMSExtension
open MzIO.IO

[<Sealed>]
type IndexRange(low: int, high: int) =
    let mutable low' =
        if low < 0 then failwith ((new ArgumentOutOfRangeException( "low may not be < 0")).ToString())
        else low
    let mutable high' =
        if high < low then failwith ((new ArgumentOutOfRangeException( "high >= low expected")).ToString())
        else high
    new() = IndexRange(0,0)
    member this.Low
        with get()              = low'
        and private set(value)  = low' <- value
    member this.High
        with get()              = high'
        and private set(value)  = high' <- value
    member this.Length = (this.High - this.Low) + 1

    /// <summary>
    /// Maps index to source array index.
    /// </summary>
    /// <param name="i"></param>
    /// <returns>Low + i</returns>

    member this.GetSourceIndex(i: int) =
        if i < 0 then failwith ((new ArgumentOutOfRangeException( "i >= 0 expected")).ToString())
        else this.Low + i

    static member EnumRange<'TItem>(items: IMzIOArray<'TItem>, range: IndexRange) =
        seq {
            for i = range.Low to range.High do
                yield items.[i]
            }

    static member EnumRange<'TItem>(items: 'TItem[], range: IndexRange) =
        IndexRange.EnumRange((items.ToMzIOArray()), range)

type BinarySearch =

    static member Search<'TItem, 'TQuery>(items:IMzIOArray<'TItem>, query:'TQuery, searchCompare: ('TItem*'TQuery) -> int, result:byref<IndexRange option>) =

        let mutable lo = 0

        let mutable hi = items.Length-1

        let mutable tmpResult = false

        while (lo <= hi) && (tmpResult = false) do
            let mid = lo + ((hi - lo) >>> 1)
            let c = searchCompare(items.[mid], query)
            if c = 0 then
                let mutable resultLow = mid
                let mutable resultHigh = mid

                //search tees low
                let rec loop i =
                    match i with
                    | value when value >= 0 ->
                            if searchCompare(items.[i], query) = 0 then
                                resultLow <- i
                                loop (i - 1)
                            else ()
                    | value when value < 0 -> ()
                loop (mid - 1)
                //search tees high
                let rec loop2 i =
                    match i with
                    | value when value < items.Length ->
                            if searchCompare(items.[i], query) = 0 then
                                resultHigh <- i
                                loop2 (i + 1)
                            else ()
                    | value when value >= items.Length -> ()
                loop2 (mid + 1)

                result <- Some (new IndexRange(resultLow, resultHigh))
                tmpResult <- true
            else
                if c < 0 then
                    lo <- mid + 1
                    result <- None
                    tmpResult <- false
                else
                    hi <- mid - 1
                    result <- None
                    tmpResult <- false
                //result <- null
        tmpResult

    static member Search<'TItem, 'TQuery>(items:IMzIOArray<'TItem>, query:'TQuery, searchCompare: ('TItem*'TQuery) -> int) =
        let mutable result = Some (new IndexRange())
        if BinarySearch.Search(items, query, searchCompare, & result) then
            IndexRange.EnumRange(items, result.Value)
        else
            Enumerable.Empty<'TItem>()

    static member Search<'TItem, 'TQuery>(items:'TItem[], query:'TQuery, searchCompare: ('TItem*'TQuery) -> int, result:byref<IndexRange option>) =
        BinarySearch.Search(items.ToMzIOArray(), query, searchCompare, & result)

    static member Search<'TItem, 'TQuery>(items:'TItem[], query:'TQuery, searchCompare: ('TItem*'TQuery) -> int) =
        BinarySearch.Search(items.ToMzIOArray(), query, searchCompare)