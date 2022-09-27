#r "nuget: FsToolkit.ErrorHandling"

open System
open System.IO

open FsToolkit.ErrorHandling
// ====================================
// Domain Types
// ====================================

type UnvalidatedLangPair = {
    LangToLearn: string
    LangIKnow: string
}

type DataReader = string -> Result<string seq, exn>

type LangToLearn = private LangToLearn of string
type LangIKnow   = private LangIKnow of string

type ClozeFileContent = ClozeFileContent of string

type ValidatedLangPair = {
    LangToLearn: LangToLearn
    LangIKnow: LangIKnow
}

type ValidationError = 
    | MissingData of name: string
    | InvalidData of name: string * value: string

type DomainError = 
    | Validation of ValidationError list
    | Exception of Exception

// ====================================
// Type Access & Validation
// ====================================

// Active Pattern for validation
let (|IsEmptyString|_|) (input: string) = 
    if input.Trim() = "" then Some () else None


module LangToLearn = 

    let value(LangToLearn lang) = lang

    let create (lang: string) = 
        match lang with
        | IsEmptyString -> Error (MissingData "Lang to Learn")
        | _ -> 
            lang.Trim()
            |> LangToLearn
            |> Ok


module LangIKnow = 

    let value(LangIKnow lang) = lang

    let create lang = 
        match lang with
        | IsEmptyString -> Error (MissingData "Lang I Know")
        | _ -> 
            lang.Trim()
            |> LangIKnow
            |> Ok

module ValidatedLangPair = 

    let validate (input: UnvalidatedLangPair) = 
        result {
    
            let! langToLearn = 
                input.LangToLearn
                |> LangToLearn.create
                |> Result.mapError List.singleton
    
            and! langIKnow = 
                input.LangIKnow
                |> LangIKnow.create
                |> Result.mapError List.singleton
    
            return {
                LangToLearn = langToLearn
                LangIKnow = langIKnow
            }
        }

    // {{c1::塞下曲::sāi xià qū}}
    // Alternative:
    // Verb être {{c1::sein}}</br>Ich bin {{c2::je suis}}</br>Du bist {{c3::tu es}}</br>
    let toClozeSingle (index) (validated: ValidatedLangPair) = 
        let (LangToLearn learn) = validated.LangToLearn
        let (LangIKnow know)    = validated.LangIKnow

        sprintf "{{c%i::%s::%s}}" index learn know


    let toClozeMulti (validatedPairs: seq<ValidatedLangPair>) = 
        validatedPairs
        // Index starts at 0, we need it to start at 1
        |> Seq.mapi (fun i line -> 
            toClozeSingle (i+1) line 
            |> sprintf "%s</br>")
        |> String.Concat
        |> ClozeFileContent

// ====================================
// Parsing CSV File
// ====================================

module Csv = 

    let readFile: DataReader = 
        fun path -> 
            try
                File.ReadLines(path) |> Ok
            with | ex -> Error ex

    let parseLine (line: string) : UnvalidatedLangPair option = 
        // Don't use RemoveEmptyEntries, otherwise "" will be removed,
        // and the line falls through the 'None' path.
        // match line.Split('|', StringSplitOptions.RemoveEmptyEntries) with
        match line.Split('|') with
        | [| langToLearn; langIKnow |] ->
            Some { LangToLearn = langToLearn; LangIKnow = langIKnow }
            
            // Match (and filter out in a later step):
            // -- Empty line
            // -- Line with more or less than two columns
        | _ -> None

    let validateRows (rows: seq<string>) = 
        rows
        |> Seq.map parseLine
        |> Seq.choose id  // Filter out None 
        |> Seq.map ValidatedLangPair.validate

    let toFile path (ClozeFileContent contentString) = 
        File.WriteAllText(path, contentString)

    let convertToCloze srcFilePath =  
        result {

            let! fileContent = 
                srcFilePath
                |> readFile
                |> Result.mapError DomainError.Exception

            let! cloze = 
                fileContent
                |> validateRows
                |> Seq.sequenceResultM
                |> Result.map ValidatedLangPair.toClozeMulti
                |> Result.mapError DomainError.Validation

            return cloze //|> toFile destFilePath
        }


// Cloze
// "{{c1::塞下曲::sāi xià qū}}</br>{{c2::唐：卢纶::tang2: lu2 lun2}}</br>{{c3::月黑雁飞高::yuè hēi yàn fēi gāo}}</br>{{c4::单于夜遁逃。::chán yú yè dùn táo。}}</br>{{c5::欲将轻骑逐::yù jiāng qīng qí zhú}}</br>{{c6::大雪满弓刀。::dà xuě mǎn gōng dāo。}}</br>"

// ====================================
// Rund the code
// ====================================

let srcPath = Path.Combine(__SOURCE_DIRECTORY__, "Input")
printfn $"Source Path: {srcPath}"
let destPath = Path.Combine(__SOURCE_DIRECTORY__, "Output")
printfn $"Destination Path: {destPath}"


let run srcDir destDir = 
    let verbFiles = Directory.EnumerateFiles srcDir

    result {
 (*        let! clozed = 
            verbFiles
            |> Seq.toList
            |> List.map(fun srcFilePath -> 
                let srcName = Path.GetFileNameWithoutExtension srcFilePath
                let destFileName = $"anki_cloze_{srcName}.txt"
                let destFilePath = Path.Combine(destDir, destFileName)

                Csv.convertToClozeFile srcFilePath destFilePath

            )
            |> List.sequenceResultM
            |> Result.map(fun _ -> ())

        // Move files
        verbFiles
        |> Seq.iter (fun srcFilePath -> 
            let donePath = Path.Combine(Path.GetDirectoryName srcFilePath, "Done")
            File.Move(srcFilePath, Path.Combine(donePath, Path.GetFileName srcFilePath), true)
        ) *)

        // Read CSV files, convert content to cloze
        // Return content with path
        let! clozed = 
            verbFiles
            |> Seq.toList
            |> List.map(fun srcFilePath -> 
                Csv.convertToCloze srcFilePath
                |> Result.map(fun cloze -> srcFilePath, cloze))
            |> List.sequenceResultM

        // Save Cloze files
        do 
            clozed
            |> Seq.iter (fun (srcFilePath, cloze) -> 
                let srcName = Path.GetFileNameWithoutExtension srcFilePath
                let destFileName = $"anki_cloze_{srcName}.txt"
                let destFilePath = Path.Combine(destDir, destFileName)
            
                Csv.toFile destFilePath cloze)

        // Move CSV files from /Input to Input/Done
        do
            clozed
            |> Seq.iter (fun (srcFilePath, _) -> 
                let donePath = Path.Combine(Path.GetDirectoryName srcFilePath, "Done")
                File.Move(srcFilePath, Path.Combine(donePath, Path.GetFileName srcFilePath), true))

        return ()
    }


run srcPath destPath






