// Learn more about F# at http://fsharp.org
module Runner
open Expecto
open Expecto.Logging
open Tests.MyTests
open Akkling


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

    runTests testConfig liteDbTests
