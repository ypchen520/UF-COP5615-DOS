module Messages

    type SharedKeyInfo = {
        UserID: int
        PublicKey: string
    }

    type SignedMessageInfo = {
        UserID: int
        Message: string
        DigitalSignature: string
    }

    type RegisterInfo = {
        UserID: int 
        UserName: string
        PublicKey: (string * string) // TODO
    }

    type ConnectInfo = {
        ReqType: string
        UserID: int 
        HashedMessage: string
        DigitalSignature: string
        HashAlgo: string  
    }

    type SubscribeInfo = {
        UserID: int
        TargetID: int
    }

    type QueryInfo = {
        ReqType: string
        UserID: int 
        TargetID: int
        TargetContent: string //UserName or HashTag
    }

    type TweetInfo = {
        UserID: int 
        UserName: string
        Tweet: string
        TargetName: string //Mentioned or Retweet
        DigitalSigned: bool
    }

    type TweetResponse = {
        ReqType: string
        UserID: int
        Tweets: array<string*string>
        TargetID: int //Mentioned or Follower
        TargetContent: string (*Connection Challenge*) // TODO
        DigitalSigned: bool
    }

    type ConfirmResponse = {
        ReqType: string
        UserID: int 
    }

    type SimulatorMsg = 
        | RegisterUser of int
        | SendTweets of int
        | SendTweetsHashtag of int
        | SendTweetsMention of int
        | SendTweetsHashtagMention of int
        | SimSubscribe of int
        | SimQueryHashtag of int
        | SimQueryMentioned of int
        | SimQuerySubscribed of int
        | SimDisconnect of int
        | SimConnect of int
        | SimFirstConnect of int
        | SimRetweet of int
        | DebuggingInfo of string
        | RequestDone of int
        | ShareKey of int
        | Done
        // | DisconnectedWS
    
    type ClientMsg =
        | Initialize of int
        | Register of int * string
        | FirstConnect of int
        | Connect of int
        | Disconnect of int
        | ShareSecretKey of int
        | Tweet of int * string
        | TweetHashtag of int
        | TweetMention of int * string
        | Retweet of int
        | Subscribe of int * int * string
        | QueryHashtag of int
        | QueryMentioned of int
        | QuerySubscribed of int
        | PrintDebuggingInfo of string
        // | DisconnectWS of bool

    let logFoldername = "log"

    let plainTweetArr = [|"What?"; "If that makes sense"; "Make the world a better place"; "Stay safe"; "Keep it real"|]
    let taggedTweetArr = [|"#COP5615 is fun"; "Black life matters #BLM "; 
                           "Stay home! #stayhome "; "Missing home #home "; 
                           "Stay #positive people!"; "But, #COP5615 is challenging"|]
    let mentionedTweetArr = [|"Implementing a Twitter clone is fun!"; 
                              "Let's fight for racial equality!"
                              "Everyone should stay home! right?";
                              "Research is fun! What do you think?"|]                       
    let hashtagsArr = [|"#COP5615"; "#BLM"; "#stayhome"; "#home"; "#positive"|]