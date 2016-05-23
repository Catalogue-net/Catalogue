namespace Catalogue

open Argu
open Chessie.ErrorHandling
open System
open System.IO

type CommandLineArgs = 
    { Docs : string
      Configuration : string }

type Arguments = 
    | [<MandatoryAttribute>] Docs of string
    | [<MandatoryAttribute>] Conf of string
    interface IArgParserTemplate with
        member this.Usage = 
            match this with
            | Docs _ -> "Specify a source directory for the documents."
            | Conf _ -> "Name of the configuration to be used for building the documents."

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
            let conf = results.GetResult(<@ Conf @>)
            ok { Docs = docsDir
                 Configuration = conf }
        with e -> 
            printfn "%s" e.Message
            Environment.Exit(-1)
            fail ""
