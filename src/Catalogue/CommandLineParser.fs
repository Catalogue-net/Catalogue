namespace Catalogue

open Argu
open Chessie.ErrorHandling
open System
open System.IO

type GlobalSettings(docs) = 
    member val Docs : string = docs
    member val Dev = false with get, set
    member val Prod = false with get, set

type Arguments = 
    | [<MandatoryAttribute>] Docs of string
    | Dev
    | Prod
    interface IArgParserTemplate with
        member this.Usage = 
            match this with
            | Docs _ -> "Specify a source directory for the documents."
            | Dev _ -> "Run the system in developer mode. Bypass optimizations."
            | Prod _ -> "Run the system in production mode. Do not bypass optimizations."

module CommandLineParser = 
    let parser = ArgumentParser.Create<Arguments>()
    
    let parseDocs d = 
        let root = getAbsolutePath d
        if not <| Directory.Exists(root) then failwith "Invalid 'docs' directory."
        root
    
    let parseArgs (args : string []) = 
        try 
            let results = parser.Parse(args)
            let docsDir = results.PostProcessResult(<@ Docs @>, parseDocs)
            let gSettings = new GlobalSettings(docsDir)
            gSettings.Dev <- results.Contains <@ Dev @>
            gSettings.Prod <- results.Contains <@ Prod @>
            ok gSettings
        with e -> 
            printfn "%s" e.Message
            Environment.Exit(-1)
            fail ""
