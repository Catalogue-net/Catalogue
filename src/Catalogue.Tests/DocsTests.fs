namespace Catalogue.Tests

open Catalogue
open Chessie.ErrorHandling
open Swensen.Unquote
open Xunit
open System.Linq
 
module DocsTests = 
    [<Fact>]
    let FrontMatterCanBeParsed() = 
        let sut = """---
key1: value1
key2: "value2:"
key3: value3
---

This article is about something else.
        """
        let res = FrontMatter.parse (sut)
        test <@ res
                |> failed
                |> not @>
        let (jObject, content) = res |> returnOrFail
        test <@ jObject.Root.Children().Count() = 3 @>
        test <@ Json.findScalar "key1" jObject = Some "value1" @>
        test <@ Json.findScalar "key2" jObject = Some "value2:" @>
        test <@ Json.findScalar "key3" jObject = Some "value3" @>
        
        // Content should not have the front matter now
        test <@ not <| content.Contains("key1") @>
        test <@ not <| content.Contains("---") @>
    
    [<Fact>]
    let FrontMatterCannotBeParsed() = 
        let sut = """
---
key1 : value1

key2: value2:
key3        : value3
---
        """
        let res = FrontMatter.parse (sut)
        test <@ res |> failed @>
    
    [<Fact>]
    let FrontMatterCannotBeParsedAsThereAreNoKeys() = 
        let sut = """---

---
        """
        let res = FrontMatter.parse (sut)
        test <@ res |> failed @>

module PartialTests = 
    [<Fact>]
    let ``Include tag can be parsed``() = 
        let sut = """any normal text followed by a blank line.

::: include abc :::
Any text can follow.
"""
        let res = BlockParsers.parseInclude (sut)
        test <@ res.Length = 1 @>
        test <@ snd res.[0] = "abc" @>
    
    [<Fact>]
    let ``Include tag will not be parsed due to missing blank line before the tag``() = 
        let sut = """No blank line provided.
::: include abc :::
Any text can follow.
"""
        let res = BlockParsers.parseInclude (sut)
        test <@ res.Length = 0 @>
    
    [<Fact>]
    let ``Render tag can be parsed``() = 
        let sut = """any normal text followed by a blank line.

::: render card 
datafile : test-file
heading : this is card heading
:::
Any text can follow.
"""
        let res = BlockParsers.parseRender (sut)
        test <@ res.Length = 1 @>
        test <@ res.[0].PartialName = "card" @>
        test <@ res.[0].Data.Contains("datafile : test-file") @>

module JsonYamlTests =
    [<Fact>]
    let ``Yaml can be parsed``() =
        let sut = """
firstname : firstname1
lastname : lastname1
items:
    - part_no:   A4786
      descrip:   Water Bucket (Filled)
      price:     1.47
      quantity:  4

    - part_no:   E1628
      descrip:   High Heeled ""Ruby"" Slippers
      price:     100.27
      quantity:  1

"""
        let result = Yaml.parseToJObject(sut)
        test <@ Json.findScalar "firstname" result = Some("firstname1") @>
        test <@ Json.findScalar "lastname" result = Some("lastname1") @>
        test <@ Json.findScalar "nonExisting" result = None @>
        test <@ Json.findScalar "items" result = None @>
        

