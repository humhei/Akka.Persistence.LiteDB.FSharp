module Tests.MyTests
open Expecto
open Akkling
open Akkling.Persistence
open System
open System.IO
open Akka.Actor
open System.Threading.Tasks
open Akka.Persistence
open System.Threading

let pass() = Expect.isTrue true "passed"
let fail() = Expect.isTrue false "failed"

type CounterChanged =
    { Delta : int }

type CounterCommand =
    | Inc
    | Dec
    | GetState

[<RequireQualifiedAccess>]
type CounterMessage =
    | Command of CounterCommand
    | Event of CounterChanged



let MyTests =
  testList "MyTests" [
    testCase "journal tests" <| fun _ -> 
        File.Delete "hello.db"

        let testInSystem expectedNumber =
            let system = 

                let config = Configuration.parse """
                    akka.persistence.journal.plugin = "akka.persistence.journal.litedb.fsharp"
                    akka.persistence.journal.litedb.fsharp {
                        class = "Akka.Persistence.LiteDB.FSharp.LiteDBJournal, Akka.Persistence.LiteDB.FSharp"
                        plugin-dispatcher = "akka.actor.default-dispatcher"
                        connection-string = "hello.db"
                    }
                """

                System.create "persistence" config

            let counter =
                spawn system "counter-1" <| propsPersist(fun mailbox ->
                    let rec loop state =
                        actor {
                            let! msg = mailbox.Receive()
                            match msg with
                            | CounterMessage.Event(changed) -> 
                                return! loop (state + changed.Delta)
                            | CounterMessage.Command(cmd) ->
                                match cmd with
                                | GetState ->
                                    mailbox.Sender() <! state
                                    return! loop state
                                | Inc -> return Persist (CounterMessage.Event { Delta = 1 })
                                | Dec -> return Persist (CounterMessage.Event { Delta = -1 })
                        }
                    loop 0)
        


            counter <! CounterMessage.Command Inc
            counter <! CounterMessage.Command Inc
            counter <! CounterMessage.Command Dec

            async { let! reply = counter <? CounterMessage.Command GetState
                    Expect.equal reply expectedNumber "passed" } |> Async.RunSynchronously

            system.Terminate() |> Task.WaitAll

        testInSystem 1
        testInSystem 2
        
    testCase "snapshot tests" <| fun _ -> 
        File.Delete "hello2.db"

        let testInSystem expectedNumber =
            let system = 

                let config = Configuration.parse """
                    akka.persistence.snapshot-store.plugin = "akka.persistence.snapshot-store.litedb.fsharp"
                    akka.persistence.snapshot-store.litedb.fsharp {
                        class = "Akka.Persistence.LiteDB.FSharp.LiteDBSnapshotStore, Akka.Persistence.LiteDB.FSharp"
                        plugin-dispatcher = "akka.actor.default-dispatcher"
                        connection-string = "hello2.db"
                    }
                """

                System.create "persistence" config

            let counter =
                spawn system "counter-1" <| propsPersist(fun ctx ->

                    let rec loop state =
                        actor {
                            let! msg = ctx.Receive() : IO<obj>
                            match msg with
                            | SnapshotOffer snap ->
                                return! loop snap

                            | :? CounterChanged as changed -> 
                                let newState = state + changed.Delta
                                let saveSnapshot = SaveSnapshot(SnapshotMetadata(ctx.Pid, ctx.LastSequenceNr()), newState)
                                let result = 
                                    (ctx.SnapshotStore.Ask saveSnapshot).Result
                                    |> unbox

                                assert (result = 0)
                                return! loop newState

                            | :? CounterCommand as cmd ->
                                match cmd with
                                | GetState ->
                                    let sender = ctx.Sender()
                                    sender <! state
                                    return! loop state
                                | Inc -> 
                                    return Persist (box { Delta = 1 })
                                | Dec -> return Persist (box { Delta = -1 })

                            | _ -> return Unhandled
                        }
                    loop 0)
                |> retype
        
            counter <! Inc
            counter <! Inc
            counter <! Dec

            async { let! reply = counter <? GetState
                    Expect.equal reply expectedNumber "passed" } |> Async.RunSynchronously

            system.Terminate() |> Task.WaitAll
        testInSystem 1
        testInSystem 2


  ]