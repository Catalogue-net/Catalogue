namespace Catalogue.Tests

open Catalogue
open Chessie.ErrorHandling
open Swensen.Unquote
open System.Dynamic
open Xunit
open FSharp.Interop.Dynamic

type HandlebarTests() =
    let engine = JavaScriptEngine.GetHandlebars()

    [<Fact>]
    member __.TemplateShouldCompile() =
        let sut = """
<div class="entry">
  <h1>{{title}}</h1>
  <div class="body">
    {{body}}
  </div>
</div>
        """
        engine.Compile("test", sut) |> ignore
        let data = new ExpandoObject()
        data?title <- "It worked"
        data?body <- "Great"
        let result = engine.TransformExpando("test", data)
        test <@ result.Contains("It worked") @>
        test <@ result.Contains("Great") @>
    
    [<Fact>]
    member __.ShouldNotCompile() =
        let sut = """
<div class="entry">
  <h1>{{title}}</h1>
  <div class="body">
    {{body}}
  </div>
</div>
        """
        let data = new ExpandoObject()
        data?title <- "It worked"
        data?body <- "Great"
        let result = engine.TransformExpando("test", data)
        test <@ result.Contains("Template:test not found.") @>
    
    [<Fact>]
    member __.CanRegisterAndUsePartials() =
        let sut = """{{> test}}"""
        engine.RegisterPartial("test", "hello") |> ignore
        engine.Compile("test1", sut) |> ignore
        let data = new ExpandoObject()
        data?title <- "It worked"
        data?body <- "Great"
        let result = engine.TransformExpando("test1", data)
        test <@ result.Contains("hello") @>

    [<Fact>]
    member __.CompareHelperWorks() =
        let sut = """
{{#compare "Test" "==" "Test"}}
true
{{/compare}}
        """
        engine.Compile("test4", sut) |> ignore
        let data = new ExpandoObject()
        let result = engine.TransformExpando("test4", data)
        test <@ result.Contains("true") @>

    [<Fact>]
    member __.CompareHelperNegativeWorks() =
        let sut = """
{{#compare "Test" "===" "Test1"}}
true
{{else}}
false
{{/compare}}
        """
        engine.Compile("test5", sut) |> ignore
        let data = new ExpandoObject()
        let result = engine.TransformExpando("test5", data)
        test <@ result.Contains("false") @>
        