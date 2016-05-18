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
               | Bad err -> printWarning "File: %s not found in the _includes folder. Page id: %s" includeFileName page.Id)

    [<CLIMutable>]
    type LinkMatch =
        {
            [<JsonIgnore>]
            MatchedText : string
            [<JsonIgnore>]
            PageId : string
            [<JsonIgnore>]
            AnchorId: string
            [<JsonPropertyAttribute("title")>]
            mutable Title: string
            [<JsonPropertyAttribute("url")>]
            mutable Link : string
        }
        static member Create(mt, pageId, anchor, title) =
            {
                MatchedText = mt
                PageId = pageId
                AnchorId = anchor
                Title = title
                Link = ""
            }

    (* Matches the links block in the input file
    A simple example is

        [[id-of-page#anchor|Title]]
        
        [\n| ]\[\[([\w_-]+)(#[\w_\-\/]+)?(\|([\w ]+))?]]
        Capture group 1: link-id
        Capture group 2: anchor-id
        Capture group 4: title    
    *)
    let linksRegex = 
        new Regex(@"[\n| ]\[\[([\w_-]+)(#[\w_\-\/]+)?(\|([\w ]+))?]]", RegexOptions.Compiled ||| RegexOptions.CultureInvariant)
    
    /// Parses the input for include tag
    let parseLinks (content : string) = 
        linksRegex.Matches(content)
        |> Seq.cast
        |> Seq.map (fun (m : Match) -> 
            LinkMatch.Create(m.Groups.[0].Value, m.Groups.[1].Value, m.Groups.[2].Value, m.Groups.[4].Value))
        |> Seq.toArray
    
    /// Processes an include block by updating the page content
    let processLinks (context : Context) (page : FrontMatter) = 
        context.HandlebarsContext.Remove("internallinks") |> ignore
        let links = new JArray()
        page.Content.ToString()
        |> parseLinks
        |> Array.iter (fun linkMatch -> 
               match context.LinkMap.TryFind(linkMatch.PageId) with
               | Some(fm) ->
                    linkMatch.Title <- 
                        if String.IsNullOrWhiteSpace linkMatch.Title then
                            fm.Title
                        else
                            linkMatch.Title
                    linkMatch.Link <- sprintf "%s%s" fm.Permalink linkMatch.AnchorId
                    page.Content.Replace(linkMatch.MatchedText, sprintf " [%s](%s)" linkMatch.Title linkMatch.Link) |> ignore
                    // Add the link to the pageContext so that it can be used by the see also section
                    links.Add(JToken.FromObject(linkMatch))
               | None -> printWarning "Page id: %s. Unable to find page with id %s to generate a link." page.Id linkMatch.PageId)
        if links.Count <> 0 then
            context.HandlebarsContext.Add("internallinks", links)

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
                    printWarning "Page id: %s. Partial: %s not found in the _partials folder." page.Id block.PartialName
            | None -> 
                printWarning "Page id: %s. No data specified for the render tag containing partial: %s. Data: %s." page.Id block.PartialName (block.Data.TrimEnd())
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
    let processPreMarkDownBlocks (context : Context) (page : FrontMatter) = 
        processInclude context page
        processLinks context page

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

