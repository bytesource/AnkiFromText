open System
open System.IO

let verbsDir = Path.Combine(".", "TwoColumsCloze", "Input")

let verbFiles = Directory.EnumerateFiles(verbsDir)

verbFiles
|> Seq.iter (fun f -> 
    let name = Path.GetFileNameWithoutExtension(f)
    printfn "%s" name
)