#r "nuget: FSharp.Data"

open System
open System.IO

open FSharp.Data

// FIXME:
// Cedict returns pinyin with 'ü' as 'u:e'
// Example: qin1 lu:e4

// Vocab 17, 18
// https://writer.zoho.com/writer/open/bkn1z6227cbb775df44da91c7a377e54e524c
let lesson   = "19"
let baseName = "5th_grade_1st_semester_lesson_"
let ext      = ".txt"
let sourceFile = baseName + lesson + ext

let destinationFile = baseName + lesson + ".csv"

module Types = 

    [<RequireQualifiedAccess>]
    type Chinese = private { Chinese: string }
    with
        static member Of(text) = { Chinese = text }
        member this.Text = this.Chinese

    [<RequireQualifiedAccess>]
    type English = private { English: string }
    with
        static member Of(text) = { English = text }
        member this.Text = this.English

    [<RequireQualifiedAccess>]
    type Pinyin = private { Pinyin: string }
    with
        static member Of(text) = { Pinyin = text }
        member this.Text = this.Pinyin


module Cedict = 
    open Types

    [<Literal>]
    let Path = __SOURCE_DIRECTORY__ + @"/assets/cedict_formatted_reversed.txt"
    
    let coreSentenceProvider = 
        CsvProvider<Path, "\t",
                    HasHeaders=false,
                    Schema="Chinese, Pinyin, English">.GetSample()

    
    let pinyinEnglishMap = 
        coreSentenceProvider.Rows
        |> Seq.map (fun row -> 
                Chinese.Of row.Chinese, (Pinyin.Of row.Pinyin, English.Of row.English))
        |> Map.ofSeq


    let formatEnglish (english: English) = 
        // [(lead) pencil; CL:支[zhi1],枝[zhi1],桿|杆[gan3]]
        let firstTranslation = 
            english.Text.Trim([| ']'; '['; ' ' |]).Split(';').[0]

        firstTranslation.Trim() |> English.Of

    
    let tryLookupPinyinEnglish (chinese: Chinese) = 
        pinyinEnglishMap 
        |> Map.tryFind chinese


open Types


let getChineseWords filename = 
    let path = "./Vocab/new/" |> Path.GetFullPath
    File.ReadAllLines(path + filename)


let words filename= 
    filename
    |> getChineseWords
    |> Array.map Chinese.Of


let wordsWithPinyinEnglish filename = 
    words filename
    |> Array.map (fun word ->
        let (pinyin, english) = 
            word
            |> Cedict.tryLookupPinyinEnglish
            |> Option.defaultValue (Pinyin.Of "", English.Of "")

        word, pinyin, english |> Cedict.formatEnglish
    )



let toFile filename (coll: seq<(Chinese * Pinyin * English)>) =
    let path = "./Vocab/ready/" |> Path.GetFullPath
    let fullPath = path + filename

    let data = 
        coll
        |> Seq.map (fun (chinese, pinyin, english) -> 
            seq { chinese.Text; pinyin.Text; english.Text }
            |> String.concat ",")

    File.WriteAllLines(fullPath, data)


sourceFile
|> wordsWithPinyinEnglish
|> toFile destinationFile




    