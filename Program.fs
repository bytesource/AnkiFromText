// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp

open System
open System.IO
open FSharp.Data

let filename = "lesson 6.csv"

let baseUrl = "http://www.jukuu.com/search.php"

let file = File.ReadAllLines(filename)

let sourroundTargetWithTag target tag sentence =
    let regex text = Text.RegularExpressions.Regex(text)
    regex(target).Replace(sentence, (sprintf "<%s>%s</%s>" tag target tag))

let replaceWith (target:string) (replacement: string) (sentence: string) = 
    let regex text = Text.RegularExpressions.Regex(text)
    regex(target).Replace(sentence, replacement)

    

// FIXME: Account for empty array.
// FIXME: Example sentences from website could contain a comma: 房间里暖洋洋的,很舒服。
// The part after the comma gets ignored by Anki:
// Importing complete.
// '暖洋洋 nuǎn yángyáng warm 房间里<b>暖洋洋</b>的 很舒服。' had 5 fields, expected 4
// 15 notes added, 0 notes updated, 0 notes unchanged.
let addSentence = [|
    for line in file do
        let character = line.Split(',').[0].Trim()
        let url = baseUrl + "?q=" + character
        async {
            let! page = FSharp.Data.HtmlDocument.AsyncLoad(url)
            printfn "Getting page %s..." url

            let allSentences (page: FSharp.Data.HtmlDocument): FSharp.Data.HtmlNode list =
                page.CssSelect("tr.c td")

            let sentence = 
                page
                |> allSentences
                |> List.map (fun node -> node.InnerText().Trim())
                |> List.filter (String.IsNullOrEmpty >> not) // First td below tr is always empty
                |> function
                    | [] -> ""
                    | s  -> 
                        s 
                        // Quick fix to remove commas in example sentence.
                        |> List.map (replaceWith "," "-")
                        |> List.minBy String.length
                        |> sourroundTargetWithTag character "b"

            return line + "," + sentence
        }
|]



[<EntryPoint>]
let main argv =
    let content =  
        Async.Parallel addSentence
        |> Async.RunSynchronously

    File.WriteAllLines(filename + "__with_sentences.csv", content)

    0 // return an integer exit code