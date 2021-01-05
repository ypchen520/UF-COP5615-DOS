open System
// open Database
// open Server
open Client
open Simulator
open TwitterClone
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
// open Akka.TestKit
open System.Diagnostics
open FSharp.Data
open FSharp.Data.JsonExtensions

let checkPeriod = 1000
let waitTime = 100


let handlerActor (mailbox: Actor<string>) = 
    let rec loop() =    
        actor {
            let! message = mailbox.Receive()
            let info = JsonValue.Parse(message)
            let req = info?req.AsString()
            match req with
            | "Start" ->
                printfn "[Local system] server is running"
            | _ ->
                printfn "[handlerActor] Unknown pattern"
            return! loop()
        }
    loop()

let zipf(x: int, n: int, alpha: int): double = 
    let a: double = double(x) ** double (alpha)
    let mutable s: double = double(0)
    for i = 0 to n-1 do
        let b: double = (double(1) / double(i + 1)) ** double(alpha)
        s <- s + b
    let ans: double = double(1) / (a * s)
    ans

let getZipfDistribution(n: int, alpha: int): int [] =
    let mutable arr: int [] = Array.empty
    for i = 1 to n do
        let x = zipf(i, n, alpha)
        let intX: int = int(round(x * double(n)))
        arr <- Array.append arr [|intX|]
    arr

    // let arr = getZipfDistribution(numU, 1)

[<EntryPoint>]
let main argv =
    try 
        let timer = new System.Diagnostics.Stopwatch()
        let numberOfUser: int = argv.[0] |> int
        let mode: string = argv.[1] |> string
        let mutable debugging: string = argv.[2] |> string
        let simulator = spawn system "simulator" simulatorActor
        let localHandler = spawn localSystem "LocalHandler" handlerActor
        let req = getReq("Start", -1, -1, "", [|("", "")|])
        remoteServer <! req
        let mutable flagRegister: bool = false
        let mutable flagSubscribe: bool = false
        let mutable flagDisconnect: bool = false
        let mutable flagConnect: bool = false
        let mutable flagSendTweet: bool = false
        let mutable flagHashTag: bool = false
        let mutable flagMention: bool = false
        let mutable flagQHashTag: bool = false
        let mutable flagQMention: bool = false
        let mutable flagQSubscribe: bool = false
        let mutable flagRetweet: bool = false
        let numCelebrity: int = int(ceil(double(numberOfUser) / double(50)))
        // printfn "numCelebrity %A" numCelebrity
        let numFollower: int = int(ceil(double(numberOfUser) / double(numCelebrity)))
        // printfn "numFollower %A" numFollower
        let numTweet: int = numberOfUser
        // printfn "numTweet %A" numTweet
        let numMention: int = int(ceil(double(numberOfUser) / double(10)))
        // printfn "numMention %A" numMention
        let numHashtag: int = int(ceil(double(numberOfUser) / double(4)))
        // printfn "numHashtag %A" numHashtag
        let numDisconnect: int = int(ceil(double(numberOfUser) / double(10)))
        // printfn "numDisconnect %A" numDisconnect
        let numQMention: int = int(ceil(double(numberOfUser) / double(10)))
        // printfn "numQMention %A" numQMention
        let numQHashtag: int = int(ceil(double(numberOfUser) / double(4)))
        // printfn "numQHashtag %A" numQHashtag
        let numQSubscribe: int = int(ceil(double(numberOfUser) / double(10)))
        // printfn "numQSubscribe %A" numQSubscribe
        let numRetweet: int = int(ceil(double(numberOfUser) / double(20)))
        // printfn "numRetweet %A" numRetweet
        let mutable requestNum: int = (numCelebrity * numFollower) + numTweet + numMention + numHashtag + (numDisconnect * 2)
                                      + numQMention + numQHashtag + numQSubscribe + numRetweet
        // printfn "requestNum %A" requestNum
        timer.Start()
        let mutable lastTimestamp = timer.ElapsedMilliseconds
        match mode with
        | "testing" ->
            requestNum <- requestNum + (numCelebrity * numFollower)
            printfn "Number of requests: %A" requestNum
            while true do
                if not flagRegister then
                    simulator <! RegisterUser numberOfUser
                    Threading.Thread.Sleep(waitTime)
                    flagRegister <- true
                if not flagSubscribe then
                    for i = 0 to numCelebrity-1 do
                        simulator <! SubscribeToOne numFollower
                        Threading.Thread.Sleep(waitTime)
                    flagSubscribe <- true
                if not flagDisconnect then    
                    simulator <! Disconnect numDisconnect
                    Threading.Thread.Sleep(waitTime)
                    flagDisconnect <- true
                if not flagSendTweet then    
                    simulator <! SendTweets numTweet
                    Threading.Thread.Sleep(waitTime)
                    flagSendTweet <- true
                if not flagHashTag then    
                    simulator <! SendTweetsHashtag numHashtag
                    Threading.Thread.Sleep(waitTime)
                    flagHashTag <- true
                if not flagConnect then    
                    simulator <! Connect numDisconnect
                    Threading.Thread.Sleep(waitTime)
                    flagConnect <- true
                if not flagMention then    
                    simulator <! SendTweetsMention numMention
                    Threading.Thread.Sleep(waitTime)
                    flagMention <- true
                if not flagQHashTag then    
                    simulator <! QueryHashtag numQHashtag
                    Threading.Thread.Sleep(waitTime)
                    flagQHashTag <- true
                if not flagQMention then    
                    simulator <! QueryMentioned numQMention
                    Threading.Thread.Sleep(waitTime)
                    flagQMention <- true
                if not flagRetweet then    
                    simulator <! Retweet numRetweet
                    Threading.Thread.Sleep(waitTime)
                    flagRetweet <- true
                if not flagQSubscribe then    
                    simulator <! QuerySubscribed numQSubscribe
                    Threading.Thread.Sleep(waitTime)
                    flagQSubscribe <- true
                if debugging = "true" then
                    simulator <! DebuggingInfo debugging
                    Threading.Thread.Sleep(waitTime)
                    debugging <- "false"
                if (timer.ElapsedMilliseconds - lastTimestamp) >= int64(checkPeriod) then
                    lastTimestamp <- timer.ElapsedMilliseconds
                    simulator <! Done
                    Threading.Thread.Sleep(waitTime) 
        | "zipf" ->
            let zipfDist: int[] = getZipfDistribution(numberOfUser, 1)
            for i = 0 to numCelebrity-1 do
                // printfn "number of follower: %A" zipfDist.[i]
                requestNum <- requestNum + zipfDist.[i]
            printfn "Number of requests: %A" requestNum
            while true do
                if not flagRegister then
                    simulator <! RegisterUser numberOfUser
                    Threading.Thread.Sleep(waitTime)
                    flagRegister <- true
                if not flagSubscribe then
                    for i = 0 to numCelebrity-1 do
                        simulator <! SubscribeToOne zipfDist.[i]
                        // printfn "number of follower: %A" zipfDist.[i]
                        Threading.Thread.Sleep(waitTime)
                    flagSubscribe <- true
                if not flagDisconnect then    
                    simulator <! Disconnect numDisconnect
                    Threading.Thread.Sleep(waitTime)
                    flagDisconnect <- true
                if not flagSendTweet then    
                    simulator <! SendTweets numTweet
                    Threading.Thread.Sleep(waitTime)
                    flagSendTweet <- true
                if not flagHashTag then    
                    simulator <! SendTweetsHashtag numHashtag
                    Threading.Thread.Sleep(waitTime)
                    flagHashTag <- true
                if not flagConnect then    
                    simulator <! Connect numDisconnect
                    Threading.Thread.Sleep(waitTime)
                    flagConnect <- true
                if not flagMention then    
                    simulator <! SendTweetsMention numMention
                    Threading.Thread.Sleep(waitTime)
                    flagMention <- true
                if not flagQHashTag then    
                    simulator <! QueryHashtag numQHashtag
                    Threading.Thread.Sleep(waitTime)
                    flagQHashTag <- true
                if not flagQMention then    
                    simulator <! QueryMentioned numQMention
                    Threading.Thread.Sleep(waitTime)
                    flagQMention <- true
                if not flagRetweet then    
                    simulator <! Retweet numRetweet
                    Threading.Thread.Sleep(waitTime)
                    flagRetweet <- true
                if not flagQSubscribe then    
                    simulator <! QuerySubscribed numQSubscribe
                    Threading.Thread.Sleep(waitTime)
                    flagQSubscribe <- true
                if (timer.ElapsedMilliseconds - lastTimestamp) >= int64(checkPeriod) then
                    lastTimestamp <- timer.ElapsedMilliseconds
                    simulator <! Done 
                    Threading.Thread.Sleep(waitTime) 
        | _ ->
            printfn "[Proj4] Unknown mode"
    with :? IndexOutOfRangeException ->
        printfn "Wrong number of input arguments! Please refer to the following format:"
        printfn "dotnet run NUMBER_OF_SIMULATED_CLIENTS MODE DEBUG"

    0 // return an integer exit code