module Util
    open Messages
    open System
    open System.Security.Cryptography
    open Akka.Actor
    open Akka.FSharp
    open FSharp.Json
    
    let system = ActorSystem.Create("TwitterClone")
    
    let strToBytes (strResponse: string) = 
        let byteResponse =
            strResponse
            |> Text.Encoding.ASCII.GetBytes
        byteResponse

    let Base64StrToBytes (strResponse: string) = 
        let byteResponse =
            strResponse
            |> Convert.FromBase64String
        byteResponse

    let bytesToBase64Str (byteResponse: byte []) = 
        let strResponse =
            byteResponse
            |> Convert.ToBase64String
        strResponse
    
    let timePadding (message: byte []) = 
        let unix = DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds() 
        let bTime = string(unix) |> strToBytes
        Array.append message bTime

    let hashSHA256 (message: byte []) = 
        message |> SHA256.HashData

    let selectActor(actorType: string, id: string): ActorSelection = 
        let actorRef = select ("akka://TwitterClone/user/"+ actorType + id) system
        actorRef
    
    let getRandNum(upper: int) = 
        let rand = Random()
        let num = rand.Next(upper)
        num
    
    let isThereIndex(content: string, pattern: string): int = 
        let found = content.IndexOf(pattern)
        found

    let isThere(targets, target): bool = 
        let found = targets |> Seq.exists (fun x -> x = target)
        found

    let getMentionTweet(targetName: string) =
        let mentioned = "@" + targetName + " "
        let randIdx = getRandNum(4)
        let mutable tweet: string = ""
        match randIdx with
        | 0 ->
            tweet <- "Implementing a Twitter clone is fun! " + mentioned + "What do you think?"
        | 1 ->
            tweet <- "Let's fight for racial equality! " + mentioned 
        | 2 -> 
            tweet <-mentioned + "Everyone should stay home! right?"
        | 3 -> 
            tweet <-"Research is fun! " + mentioned + "What do you think?"
        | _ ->
            printfn "[getMentionTweet] Unknown pattern"
        tweet

    let getRegisterRequest(uid, uName, publicKey): string =
        let data: RegisterInfo = {
            UserID = uid;
            UserName = uName;
            PublicKey = publicKey;
        }
        let reqString: string = Json.serialize data
        reqString
    
    let getTweetsRequest(uid: int, 
                         uName: string, 
                         tweet:string, 
                         targetName: string, 
                         digitalSigned: bool): string =
        let data: TweetInfo = {
            UserID = uid; 
            UserName = uName; 
            Tweet = tweet;
            TargetName = targetName;
            DigitalSigned = digitalSigned
        }
        let reqString: string = Json.serialize data
        reqString

    let getConnectRequest(req: string, uid: int, hashedMsg: string, digiSignature: string, hashAlgo: string): string = 
        let data: ConnectInfo = {
            ReqType = req; 
            UserID = uid; 
            HashedMessage = hashedMsg;
            DigitalSignature = digiSignature;
            HashAlgo = hashAlgo 
        }
        let reqString: string = Json.serialize data
        reqString
    
    let getSubscribeRequest(uid: int, targetID: int): string = 
        let data: SubscribeInfo = {
            UserID = uid; 
            TargetID = targetID
        }
        let reqString: string = Json.serialize data
        reqString

    let getQueryRequest(req: string, uid: int, targetID: int, targetContent: string): string = 
        let data: QueryInfo = {
            ReqType = req; 
            UserID = uid; 
            TargetID = targetID;
            TargetContent = targetContent
        }
        let reqString: string = Json.serialize data
        reqString
    
    let getSharedKeyRequest(uid: int, publicKey: string): string =
        let data: SharedKeyInfo = {
            UserID = uid;
            PublicKey = publicKey;
        }
        let reqString: string = Json.serialize data
        reqString

    let getSignedMessageRequest(uid: int, message: string, digitalSignature: string): string =
        let data: SignedMessageInfo = {
            UserID = uid;
            Message = message;
            DigitalSignature = digitalSignature;
        }
        let reqString: string = Json.serialize data
        reqString

    let getHMACSignature (message: string, key: byte []) =
        let hmac: HMACSHA256 = new HMACSHA256(key)
        message |> strToBytes |> hmac.ComputeHash