#r "nuget: CHTCHSConv"

open System
open System.IO

// FIXME: Convert traditional Chinese to simplified Chinese
// FIXME: Verify that we download the dictionary only once.

#load "ChineseToAnki.fsx"

open ChineseToAnki
open Types

fsi.PrintLength <- 5000
fsi.ShowDeclarationValues <- false

module Helpers = 
    // https://www.nuget.org/packages/CHTCHSConv/
    open Microsoft.International.Converters.TraditionalChineseToSimplifiedConverter

    let toSimplified word = 
        ChineseConverter.Convert(word, ChineseConversionDirection.TraditionalToSimplified)


module Names = 
    // Luka语文5年级::上半年::Lesson 6
    let batchFilename = "6th_grade_1st_semester.txt"
    let batchSrcPath  = [ "Vocab"; "new"; "batch"];

    let ankiPath  = [ "Vocab"; "ready"; "fromBatch" ]
    let ankiFileBase  = "6th_grade_1st_semester_lesson "
    //let ankiFileBase  = "Luka语文5年级::下半年::Lesson "
    let ankiFileEnding = ".csv"

    let ankiFilename num extra = 
        let extraStr = extra |> Option.defaultValue ""
        sprintf $"%s{ankiFileBase}%i{num}%s{extraStr}%s{ankiFileEnding}"


module IO =
    let stitchPath pathList name = 
        // FIXME: How to combine these a little nicer?
        __SOURCE_DIRECTORY__ :: pathList @ [ name ]
        |> Array.ofList
        |> Path.Combine


    let getFile pathList name = 
        stitchPath pathList name
        |> File.ReadAllText


(*  Required format for batch file:
18 
码头
笼罩
===
19 
仪态
端庄
===
22 
附庸
团结
*)
module Batch = 

    let rawContent() = IO.getFile Names.batchSrcPath Names.batchFilename

    let toMap() =
        let stringBlocks = rawContent().Split("===", StringSplitOptions.RemoveEmptyEntries)

        [ for lessonBlock in stringBlocks do 

            let lessonList = lessonBlock.Trim().Split(Environment.NewLine,
                                                      StringSplitOptions.RemoveEmptyEntries)
            let num   = lessonList[0] |> int
            let vocab = 
                lessonList[1..]
                |> Array.map (fun chinese -> 
                    chinese.Trim()
                    // NOTE: Google Photo App OCR mistakenly interpreted
                    // some characters as traditional Chinese characters.
                    |> Helpers.toSimplified
                    |> Chinese.Of)

            num, vocab // num = lesson number
        ]
        |> Map.ofList


module Vocab = 

    let withTranslations cedict batchMap = 
        batchMap
        |> Map.map (fun _ vocab -> 
        vocab |> wordsWithPinyinEnglish cedict)


    let toFile fullPath (coll: seq<(Chinese * Pinyin * English)>) =
        let data = 
            coll
            |> Seq.map (fun (chinese, pinyin, english) -> 
                seq { chinese.Text; pinyin.Text; english.Text }
                |> String.concat ",")
    
        File.WriteAllLines(fullPath, data)


module App = 

    let isPinyinEmpty (pinyin: Pinyin) = 
        pinyin.Text = String.Empty
    
    // Calculate how many words could not be found in cedit.
    // In this case Pinyin "xxx" is empty.
    let missingTranslationCount translations = 
        translations
        |> Seq.filter(fun (_, pinyin, _) -> pinyin |> isPinyinEmpty)
        |> Seq.length

    let cedict = Cedict.pinyinEnglishMap()

    let fromBatchToVocabFiles() = 
        Batch.toMap()
        |> Vocab.withTranslations cedict
        |> Map.iter (fun num translations -> 
            let missing = translations |> missingTranslationCount
            let countStr = if missing = 0 then None else Some $"_{missing}" 
            let name = Names.ankiFilename num countStr
            let fullPath = IO.stitchPath Names.ankiPath name
            Vocab.toFile fullPath translations)
        

App.fromBatchToVocabFiles()



