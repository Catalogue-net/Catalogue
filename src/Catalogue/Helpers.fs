namespace Catalogue

open System.IO
open System.Linq
open System
open System.Reflection
open System.Text
open Microsoft.FSharp.Core.Printf
open Chessie.ErrorHandling
open Fake

[<AutoOpen>]
module PrintHelpers = 
    let successExitCode = 0
    let errorExitCode = 1
    let warningExitCode = 2

    // Colored printf
    /// Taken from : https://blogs.msdn.microsoft.com/chrsmith/2008/10/01/f-zen-colored-printf/       
    let cprintfn c fmt = 
        Printf.kprintf (fun s -> 
            let old = System.Console.ForegroundColor
            try 
                System.Console.ForegroundColor <- c
                System.Console.WriteLine s
            finally
                System.Console.ForegroundColor <- old) fmt
    
    let printWarning fmt = 
        Environment.ExitCode <- warningExitCode
        cprintfn ConsoleColor.Yellow fmt
    let printError fmt = 
        Environment.ExitCode <- errorExitCode
        cprintfn ConsoleColor.Red fmt
    let printVerbose fmt = cprintfn ConsoleColor.Green fmt
    let printInfo fmt = cprintfn ConsoleColor.White fmt
    let log fmt = cprintfn ConsoleColor.White fmt

    /// Print error and exit application
    let printAndExit error = 
        printError "%s" error
        Environment.Exit(errorExitCode)
    
    /// Traces a line
    let printLine() = printVerbose "---------------------------------------------------------------------"
    
    /// Traces a header
    let printHeader name = 
        printLine()
        printVerbose "%s" name
    
    /// Traces an exception details (in red)
    let printException (ex : Exception) = exceptionAndInnersToString ex |> printError "%s"
    
[<AutoOpenAttribute>]
module ResultTypeHelpers = 
    /// Append a new error to the error results. This is helpful when
    /// we want to add a more meaningful error to the existing system error.
    let appendError (error : string) (result : Result<_, _>) = 
        match result with
        | Ok _ -> result
        | Bad errs -> fail <| sprintf "%s \n%s" error (String.Join(" ", errs))
    
    /// Overall terminator which exits the application in case of 
    /// critical error
    let printAndExit (result : Result<_, _>) = 
        let raiseExn msgs = 
            msgs
            |> Seq.map (sprintf "%O")
            |> String.concat (Environment.NewLine + "\t")
            |> fun x -> 
                printAndExit x
                x
            /// Note: This is done to make the F# type inference work correctly. Without the
            /// below it thinks we are returning unit
            |> failwith
        either fst raiseExn result

[<AutoOpen>]
module IOHelpers = 
    let rootPath = AppDomain.CurrentDomain.BaseDirectory
    let (+/) (path1 : string) (path2 : string) = Path.Combine([| path1; path2 |])
    
    let dirExists (path : string) = 
        if Directory.Exists(path) then ok path
        else fail <| sprintf "'%s' directory not found." path
    
    let loopDir (dir : string) = 
        if Directory.Exists dir then Directory.EnumerateDirectories(dir)
        else Enumerable.Empty<string>()
    
    let loopFilesExt (extension : string) (dir : string) = 
        if Directory.Exists dir then 
            Directory.EnumerateFiles(dir, sprintf "*.%s" extension, SearchOption.AllDirectories)
        else Enumerable.Empty<string>()
    
    let loopFiles (dir : string) = 
        if Directory.Exists dir then Directory.EnumerateFiles(dir)
        else Enumerable.Empty<string>()
    
    let createDir (dir : string) = Directory.CreateDirectory(dir) |> ignore
    
    let rec emptyDir path = 
        loopFiles path |> Seq.iter File.Delete
        loopDir path |> Seq.iter (fun dirPath -> 
                            emptyDir dirPath
                            Directory.Delete(dirPath, true))
    
    let delDir (path) = 
        let mutable attempt = 0
        
        let rec delete (path) = 
            if attempt <= 3 then 
                try 
                    emptyDir path
                    Directory.Delete(path, true)
                with _ -> 
                    attempt <- attempt + 1
                    delete path
        delete path
    
    let getAbsolutePath (path : string) = 
        if Path.IsPathRooted path then path
        else Path.GetFullPath((new Uri(rootPath +/ path)).LocalPath)
    
    /// Reads a given file and returns the result wrapped in a Result type    
    let readFile (path : string) = 
        try 
            ok <| File.ReadAllText(path)
        with e -> fail <| exceptionAndInnersToString e
    
    let rec directoryCopy srcPath dstPath copySubDirs = 
        if not <| System.IO.Directory.Exists(srcPath) then 
            let msg = System.String.Format("Source directory does not exist or could not be found: {0}", srcPath)
            raise (System.IO.DirectoryNotFoundException(msg))
        if not <| System.IO.Directory.Exists(dstPath) then System.IO.Directory.CreateDirectory(dstPath) |> ignore
        let srcDir = new System.IO.DirectoryInfo(srcPath)
        for file in srcDir.GetFiles() do
            let temppath = System.IO.Path.Combine(dstPath, file.Name)
            file.CopyTo(temppath, true) |> ignore
        if copySubDirs then 
            for subdir in srcDir.GetDirectories() do
                let dstSubDir = System.IO.Path.Combine(dstPath, subdir.Name)
                directoryCopy subdir.FullName dstSubDir copySubDirs

[<AutoOpen>]
module ActivePatterns = 
    let (|Endswith|_|) pattern (input : string) = 
        if input.EndsWith(pattern) then Some()
        else None
