// Learn more about F# at http://fsharp.org
module Runner
open Expecto
open Expecto.Logging
open System
open Tests.MyTests
open Akkling
open System.Diagnostics
open LiteDB



//open System
//open LiteDB
//open LiteDB.FSharp
//open LiteDB.FSharp.Extensions

//[<StructuredFormatDisplay("a{SizeGb}GB")>]
//[<CLIMutable>]
//type Disk = 
//    { SizeGb : int }

//[<StructuredFormatDisplay("Computer #{Id}: {Manufacturer}/{DiskCount}/{Disks}")>]
//[<CLIMutable>]
//type Computer =
//    { Id: int
//      Manufacturer: string
//      Disks: Disk list }
//    [<BsonIgnore>] 
//    member __.DiskCount = __.Disks.Length  // property calculated


//type Article(Name: string) =
//    // private mutable value
//    [<BsonField("Yes")>]
//    let mutable ss = 6
//    member val Id = new ObjectId() with get,set
//    member val Name = Name with get, set

let testConfig =  
    { Expecto.Tests.defaultConfig with 
         parallelWorkers = 1
         verbosity = LogLevel.Debug }  

let liteDbTests = 
    testList "All tests" [  
        MyTests
    ]

[<EntryPoint>]
let main argv = 

    //use db = new LiteDatabase("data.db")
    //let computers = db.GetCollection<Article>("article")

    //let p = new Article("myArticleName")
   
    //computers.Insert(p) |> ignore

    runTests testConfig liteDbTests
