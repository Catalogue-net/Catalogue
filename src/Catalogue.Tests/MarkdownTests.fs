namespace Catalogue.Tests

open Catalogue
open Chessie.ErrorHandling
open Swensen.Unquote
open System.Dynamic
open Xunit
open FSharp.Interop.Dynamic

module MarkDownTests = 
    let engine = JavaScriptEngine.GetMarkDownParser()
    
    [<Fact>]
    let ``Warning Tags should render``() =
        let sut = """
::: alert-warning
Hello world! [Link](#).
:::"""
        let result = engine.Render("test", sut)
        test <@ result.RenderedMarkDown.Contains("""<div class="alert alert-warning" role="alert">""") @>
        test <@ result.RenderedMarkDown.Contains("""<p>Hello world! <a href="#" class="alert-link">Link</a>.</p>""") @>

    [<Fact>]
    let ``Callout Tags should render``() =
        let sut = """
::: warning
Hello world! [Link](#).
:::"""
        let result = engine.Render("test", sut)
        test <@ result.RenderedMarkDown.Contains("""<div class="callout callout-warning">""") @>
        test <@ result.RenderedMarkDown.Contains("""<p>Hello world! <a href="#" class="alert-link">Link</a>.</p>""") @>

