open System
open System.IO

// https://docs.ankiweb.net/#/editing?id=cloze-deletion

let ext = ".txt"
let srcFilePoem = "poem_wangrongbuqu"
let srcFilePinyin = srcFilePoem + "_pinyin"


let getFile name = 
    let srcPath = "./Poems/Original/" |> Path.GetFullPath
    File.ReadAllLines(srcPath + name + ext)


let rawContent = getFile srcFilePoem
printfn "Characters: %A" rawContent
let rawPinyin  = getFile srcFilePinyin
printfn "Pinyin: %A" rawPinyin

let lookup = 
    rawPinyin
    |> Array.zip rawContent
    |> Map.ofArray


// FIXME: Don't use ", " as delimiter. Try to cut between non-Chinese-chracters using regex.
let withCloze (text: string []) =
    let del = '，'
    let delString = string del
    let mutable i = 0
    let toClozeFormat = sprintf "{{c%d::%s::%s}}" 

    let test = "".Split ','

    [|
        for line in text do
            let pinyin = 
                lookup.TryFind line 
                |> Option.defaultValue ""

            if line.Contains delString
            then 
                let pinyinPart = pinyin.Split del
                let parts =
                    line.Split del 
                    |> Array.mapi (fun index part -> i <- i + 1; toClozeFormat i part pinyinPart.[index])
                    |> String.concat delString
                yield parts
            else
                i <- i + 1
                yield toClozeFormat i line pinyin
    |]


let withLineBreak text = 
    Array.map (fun line -> line + "</br>") text


let toFile text =
    let destPath = "./Poems/Cloze/" |> Path.GetFullPath
    let dest = destPath + srcFilePoem + "_with_cloze" + ext

    File.WriteAllText(dest, text)

let withQuotes text = 
    text 
    |> String.concat ""
    |> sprintf "\"%s\"" 
    

getFile srcFilePoem
|> withCloze
|> withLineBreak
|> withQuotes
|> toFile