# Akka.Persistence.LiteDB.FSharp

Stable | Prerelease
--- | ---
[![NuGet Badge](https://buildstats.info/nuget/Akka.Persistence.LiteDB.FSharp)](https://www.nuget.org/packages/Akka.Persistence.LiteDB.FSharp/) | [![NuGet Badge](https://buildstats.info/nuget/Akka.Persistence.LiteDB.FSharp?includePreReleases=true)](https://www.nuget.org/packages/Akka.Persistence.LiteDB.FSharp/)


MacOS/Linux | Windows
--- | ---
[![CircleCI](https://circleci.com/gh/myName/Akka.Persistence.LiteDB.FSharp.svg?style=svg)](https://circleci.com/gh/myName/Akka.Persistence.LiteDB.FSharp) | [![Build status](https://ci.appveyor.com/api/projects/status/0qnls95ohaytucsi?svg=true)](https://ci.appveyor.com/project/myName/Akka.Persistence.LiteDB.FSharp)
[![Build History](https://buildstats.info/circleci/chart/myName/Akka.Persistence.LiteDB.FSharp)](https://circleci.com/gh/myName/Akka.Persistence.LiteDB.FSharp) | [![Build History](https://buildstats.info/appveyor/chart/myName/Akka.Persistence.LiteDB.FSharp)](https://ci.appveyor.com/project/myName/Akka.Persistence.LiteDB.FSharp)

---

# Get started
### Snapshot 
```fsharp
    let system =
        let config = Configuration.parse """
            akka.persistence.snapshot-store.plugin = "akka.persistence.snapshot-store.litedb.fsharp"
            akka.persistence.snapshot-store.litedb.fsharp {
                class = "Akka.Persistence.LiteDB.FSharp.LiteDBSnapshotStore, Akka.Persistence.LiteDB.FSharp"
                plugin-dispatcher = "akka.actor.default-dispatcher"
                connection-string = "hello.db"
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

```