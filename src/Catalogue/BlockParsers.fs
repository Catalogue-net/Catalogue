namespace Catalogue

open Chessie.ErrorHandling
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open System
open System.Dynamic
open System.IO
open System.Linq
open System.Text
open System.Text.RegularExpressions

module BlockParsers = 
    let partialRegex = new Regex(@"[\s]{3}{{> ([\S]+)([ ]+([\S]+)[ ]*=[ ]*([\S]+))*[ ]*}}", RegexOptions.Compiled)
    (* Matches the include block in the input file
    A simple example is

        ::: include abc.md :::
    
    Note: This expects that there is a blank line before the tag. This is done to avoid unnecessary matching.
    We can straight away capture the result of group 1.
    *)
    let includeRegex = 
        new Regex(@"[\s]{3}:::[ ]+include[ ]+([\S]+)[ ]*:::", RegexOptions.Compiled ||| RegexOptions.CultureInvariant)
    
    /// Parses the input for include tag
    let parseInclude (content : string) = 
        includeRegex.Matches(content)
        |> Seq.cast
        |> Seq.map (fun (m : Match) -> (m.Groups.[0].Value, m.Groups.[1].Value))
        |> Seq.toArray
    
    /// Processes an include block by updating the page content
    let processInclude (context : Context) (page : FrontMatter) = 
        page.Content.ToString()
        |> parseInclude
        |> Array.iter (fun (matchedText, includeFileName) -> 
               match readFile <| context.Root.Path +/ SpecialDir.includes +/ includeFileName with
               | Ok(c, _) -> page.Content.Replace(matchedText, c) |> ignore
               | Bad err -> printfn "File: %s not found in the _includes folder. Page id: %s" includeFileName page.Id)
    
    (* Matches the render block in the input file
    A simple example is
    
        ::: render test
        data: abc
        link1:blah blah
        link2: blah2
        :::
    
    Note: This expects that there is a blank line before the tag. This is done to avoid unnecessary matching.
    We can straight away capture the result of:
        - group 1 ==> Name of the partial
        - group 2 ==> YAML data to be passed to the partial
    *)
    let renderRegex = 
        new Regex(@"<p>:::[ ]+render[ ]+([\S]+)[\s]+([\S\s]+?):::</p>", 
                  RegexOptions.Compiled ||| RegexOptions.CultureInvariant)
    
    /// Represents the result of matching render tag
    type RenderBlockInfo = 
        { MatchedText : string
          PartialName : string
          Data : string }
    
    /// Parses the input for render tag
    let parseRender (content : string) = 
        renderRegex.Matches(content)
        |> Seq.cast
        |> Seq.map (fun (m : Match) -> 
               { MatchedText = m.Groups.[0].Value
                 PartialName = m.Groups.[1].Value
                 Data = m.Groups.[2].Value })
        |> Seq.toArray
    
    /// Process the input block for render blocks and replace the
    /// blocks with the rendered partials.
    let processRender (context : Context) (page : FrontMatter) = 
        let transform block = 
            let data = Yaml.parseToJObject block.Data
            
            let partialData = 
                match Json.findScalar "data-file" data with
                | Some(fileName) -> 
                    match Json.findComplex fileName context.Data with
                    | Some(d) -> Some d
                    | None -> None
                | None -> 
                    // No data-file attribute is specified so the content of 
                    // the data is the actual partial content
                    Some data
            match partialData with
            | Some d -> 
                // Find the correct partial and render it
                match context.Partials.ContainsKey(block.PartialName) with
                | true -> 
                    let partial = context.Partials.[block.PartialName]
                    let result = context.JSEngine.HandleBars.Transform(block.PartialName + "_partial", d)
                    page.Content.Replace(block.MatchedText, result) |> ignore
                | false -> 
                    printfn "Partial: %s not found in the _partials folder. Page id: %s" block.PartialName page.Id
            | None -> 
                printfn "No data specified for the render tag containing partial: %s for page: %s. BlockInfo:%A" block.PartialName
                    page.Id block
        page.Content.ToString()
        |> parseRender
        |> Array.iter transform
    
    (* Matches the diagram block in the input file
    A simple example is
    
        ::: diagram test
        graph TD
        A --> B
        B --> C
        :::
    
    Note: This expects that there is a blank line before the tag. This is done to avoid unnecessary matching.
    We can straight away capture the result of:
        - group 1 ==> Id for the diagram
        - group 2 ==> The actual code
    *)
    let diagramRegex = 
        new Regex(@"<p>:::[ ]+diagram[ ]+([\S]+)[\s]+([\S\s]+?):::</p>", 
                  RegexOptions.Compiled ||| RegexOptions.CultureInvariant)
    
    /// Represents the result of matching diagram tag
    type DiagramBlockInfo = 
        { MatchedText : string
          Id : string
          Data : string }
    
    /// Parses the input for diagram tag
    let parseDiagram (content : string) = 
        diagramRegex.Matches(content)
        |> Seq.cast
        |> Seq.map (fun (m : Match) -> 
               { MatchedText = m.Groups.[0].Value
                 Id = m.Groups.[1].Value
                 Data = m.Groups.[2].Value })
        |> Seq.toArray
    
    /// Process the input block for diagram blocks and replace the
    /// blocks with the rendered partials.
    let processDiagram (context : Context) (page : FrontMatter) = 
        let transform (block : DiagramBlockInfo) = 
            let content = sprintf """<div class="mermaid">%s</div>""" block.Data
            //let c = context.JSEngine.Diagrammer.CreateDiagram(block.Id, block.Data)
            page.Content.Replace(block.MatchedText, content) |> ignore
        page.Content.ToString()
        |> parseDiagram
        |> Array.iter transform
    
    /// Process the input for blocks and transform the page output
    let processBlocks (context : Context) (page : FrontMatter) = 
        processInclude context page

    let processPostMarkDownBlocks (context : Context) (page : FrontMatter) = 
        processDiagram context page
        processRender context page

    let private htmlRegex = new Regex("<.*?>", RegexOptions.Compiled)

    /// Removes all the HTML tags from the input. This is used before creating the search
    /// index as it can take care of some noise
    let stripHtmlTags (input: string) = htmlRegex.Replace(input, String.Empty)

    let private searchRegex = new Regex("[^a-zA-Z ]", RegexOptions.Compiled ||| RegexOptions.CultureInvariant)

    /// Removes all the HTML tags from the input and all non alpha character which are
    /// not in range [a-zA-Z ] 
    /// This is used before creating the search index as it can take care of some noise
    let stripForSearch (input: string) = 
        searchRegex.Replace(input, " ")

