﻿// Prime - A PRIMitivEs code library.
// Copyright (C) Bryan Edds, 2012-2014.

namespace Prime
open System
open System.IO
open System.Xml
open Xunit
open Prime
module Tests =

    type TestDispatcher () =
        member dispatcher.Init (xtension : Xtension, _ : IXDispatcherContainer) =
            xtension?InittedField <- 5
        member dispatcher.Dispatch (xtension : Xtension, _ : IXDispatcherContainer) =
            xtension?InittedField () * 5

    type TestDispatcherContainer () =
        let testDispatcher = (TestDispatcher ()) :> obj
        let testDispatchers = Map.singleton typeof<TestDispatcher>.Name testDispatcher
        interface IXDispatcherContainer with
            member this.GetDispatchers () = testDispatchers

    type [<CLIMutable; NoComparison>] TestXtended =
        { Xtension : Xtension }

        static member (?) (this : TestXtended, memberName) =
            fun args ->
                (?) this.Xtension memberName args

        static member (?<-) (this : TestXtended, memberName, value) =
            let xtension = Xtension.op_DynamicAssignment (this.Xtension, memberName, value)
            { this with Xtension = xtension }

    let writeToStream write source =
        let memoryStream = new MemoryStream ()
        let xmlWriterSettings = XmlWriterSettings ()
        let xmlWriter = XmlWriter.Create (memoryStream, xmlWriterSettings)
        xmlWriter.WriteStartDocument ()
        xmlWriter.WriteStartElement "Root"
        write xmlWriter source
        xmlWriter.WriteEndElement ()
        xmlWriter.WriteEndDocument ()
        xmlWriter.Flush ()
        memoryStream :> Stream

    let readFromStream read (stream : Stream) target =
        let xmlReader = XmlReader.Create stream
        let xmlDocument = let emptyDoc = XmlDocument () in (emptyDoc.Load xmlReader; emptyDoc)
        let result' = read (xmlDocument.SelectSingleNode "Root") target
        result'

    // globalization is fine since this object is stateless.
    let tdc = TestDispatcherContainer ()

    let [<Fact>] canAddField () =
        let xtn = Xtension.empty
        let xtn' = xtn?TestField <- 5
        let fieldValue = xtn'?TestField ()
        Assert.Equal (5, fieldValue)

    let [<Fact>] cantAddFieldWhenSealed () =
        let xtn = { XFields = Map.empty; OptXTypeName = None; CanDefault = true; IsSealed = true }
        Assert.Throws<Exception> (fun () -> ignore <| xtn?TestField <- 0)

    let [<Fact>] cantAccessNonexistentField () =
        let xtn = { XFields = Map.empty; OptXTypeName = None; CanDefault = false; IsSealed = true }
        let xtn' = xtn?TestField <- 5
        Assert.Throws<Exception> (fun () -> ignore <| xtn'?TetField ())

    let [<Fact>] canAddFieldViaContainingType () =
        let xtd = { Xtension = Xtension.empty }
        let xtd' = xtd?TestField <- 5
        let fieldValue = xtd'?TestField ()
        Assert.Equal (5, fieldValue)

    let [<Fact>] missingFieldReturnsDefault () =
        let xtn = Xtension.empty
        let xtn' = xtn?TestField <- 0
        let fieldValue = xtn'?MissingField ()
        Assert.Equal (0, fieldValue)

    let [<Fact>] dispatchingWorks () =
        let xtn = { Xtension.empty with OptXTypeName = Some typeof<TestDispatcher>.Name }
        let xtn' = xtn?Init (xtn, tdc)
        let dispatchResult = xtn?Dispatch (xtn', tdc)
        Assert.Equal (dispatchResult, 25)

    let [<Fact>] dispatchingFailsAppropriately () =
        let xtn = { XFields = Map.empty; OptXTypeName = Some typeof<TestDispatcher>.Name; CanDefault = true; IsSealed = true }
        Assert.Throws<Exception> (fun () -> ignore <| xtn?MissingDispatch tdc)

    let [<Fact>] xtensionSerializationWorks () =
        let xtn = { XFields = Map.empty; OptXTypeName = Some typeof<TestDispatcher>.Name; CanDefault = true; IsSealed = false }
        let xtn' = xtn?TestField <- 5
        use stream = writeToStream Xtension.writeToXmlWriter xtn'
        ignore <| stream.Seek (0L, SeekOrigin.Begin)
        let xtn'' = readFromStream (fun s _ -> Xtension.read s) stream xtn
        Assert.Equal (xtn', xtn'')

    let [<Fact>] containedXtensionSerializationWorks () =
        let xtd = { Xtension = { XFields = Map.empty; OptXTypeName = Some typeof<TestDispatcher>.Name; CanDefault = true; IsSealed = false }}
        let xtd' = xtd?TestField <- 5
        use stream = writeToStream Xtension.writePropertiesToXmlWriter xtd'
        ignore <| stream.Seek (0L, SeekOrigin.Begin)
        let xtd'' = readFromStream (fun s x -> Xtension.readProperties s x; x) stream xtd
        Assert.Equal (xtd', xtd'')