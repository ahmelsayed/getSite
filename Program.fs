open System
open ARMClient.Library
open FSharp.Interop.Dynamic
open System.Net.Http
open Newtonsoft.Json

let (>>=) = Option.bind

type Mode =
    | Create
    | Delete
    | List

let defaultResourceGroupName = "testSiteResourceGroup"
let defaultSubscription = "2d41f884-3a5d-4b75-809c-7495edb04a0f"
let websitesApiVersion = "2015-02-01"
let resourceGroupApiVersion = "2014-04-01"
let defaultServerFarm = "/subscriptions/2d41f884-3a5d-4b75-809c-7495edb04a0f/resourceGroups/Default-Web-WestUS/providers/Microsoft.Web/serverfarms/Medium"
let resourceGroupCsmTemplate = "https://management.azure.com/subscriptions/{0}/resourceGroups/{1}?api-version={2}"
let sitesCsmTemplate = "https://management.azure.com/subscriptions/{0}/resourceGroups/{1}/providers/Microsoft.Web/sites?api-version={2}"
let siteCsmTemplate = "https://management.azure.com/subscriptions/{0}/resourceGroups/{1}/providers/Microsoft.Web/sites/{2}?api-version={3}"
let sitePublishCredsCsmTemplate = "https://management.azure.com/subscriptions/{0}/resourceGroups/{1}/providers/Microsoft.Web/sites/{2}/config/publishingcredentials/list?api-version={3}"

type CsmMessage = { name: string; location: string; }
type SitePropertiesMessage = { serverFarmId: string }
type SiteMessage = { location: string; properties: SitePropertiesMessage }
type CsmMessageArray = { value: CsmMessage array }
type PublishingCredentialsProperties = { scmUri: string }
type PublishingCredential = { properties: PublishingCredentialsProperties }

let getMode =
    match Environment.GetCommandLineArgs () with
    | args when args.Length = 2 -> match args.[1].ToLower() with
                                   | "create" -> Some Create
                                   | "delete" -> Some Delete
                                   | "list" -> Some List
                                   | _ -> None
    | args when args.Length > 2 -> None
    | _ -> Some Create

let armClient =
    let temp = ARMLib.GetDynamicClient(String.Empty) :?> ARMClient.Library.ARMLib
    temp?ConfigureLogin(LoginType.Upn, "", "")
    temp


let siteNameGenerator () =
    let random = Random();
    let adjectives = [|"Blue"; "Green"; "Orange"; "Pink"; "Purple"; "Red"; "Yellow"; "Adventurous"; "Bold"; "Brave"; "Bright"; "Charming"; "Colorful"; "Cool"; "Courageous";
                       "Creative"; "Dazzling"; "Dancing"; "Eager"; "Fast"; "Fearless"; "Friendly"; "Gentle"; "Grazing"; "Happy"; "Heroic"; "Hungry"; "Important"; "Impressive"; "Ingenious"; "Insightful"; "Intuitive"; "Laughing"; "Masterful";
                       "Merry"; "Mighty"; "Mild"; "Modern"; "Outstanding"; "Smooth"; "Patient"; "Peaceful"; "Popular"; "Precise"; "Productive"; "Proud"; "Resilient"; "Resourceful"; "Running"; "Savvy"; "Sociable";
                       "Sharp"; "Shiny"; "Skillful"; "Smart"; "Speedy"; "Spirited"; "Strong"; "Stunning"; "Successful"; "Talented"; "Technical"; "Tenacious"; "Thoughtful"; "Upbeat"; "Valuable"; "Victorious";
                       "Vigorous"; "Visionary"; "Vital"; "Vivacious"; "Winning"; "Wise"; "Youthful"; "Zealous"; "Zestful"|]
    let nouns = [|"Albatross"; "Alligator"; "Ant"; "Armadillo"; "Badger"; "Bear"; "Bee"; "Bird"; "Bison"; "Bull"; "Buffalo"; "Caribou"; "Cheetah"; "Cobra";
                  "Coyote"; "Crow"; "Crab"; "Dinosaur"; "Dragon"; "Dolphin"; "Dove"; "Eagle"; "Elephant"; "Elk"; "Falcon"; "Fish"; "Fox"; "Frog"; "Gazelle"; "Panda"; "Giraffe"; "Gorilla";
                  "Hamster"; "Hawk"; "Heron"; "Hippo"; "Horse"; "Kangaroo"; "Koala"; "Kudu"; "Lemur"; "Leopard"; "Lion"; "Llama"; "Lobster"; "Manatee"; "Meerkat"; "Mink"; "Mouse";
                  "Narwhal"; "Octopus"; "Okapi"; "Oryx"; "Ostrich"; "Otter"; "Oyster"; "Panther"; "Parrot"; "Pelican"; "Penguin"; "Pony"; "Quail"; "Rabbit"; "Raven"; "Rhino"; "Robin";
                  "Salmon"; "Seahorse"; "Seal"; "Sparrow"; "Spider"; "Squid"; "Squirrel"; "Starling"; "Stork"; "Swan"; "Tiger"; "Trout"; "Turkey"; "Turtle"; "Unicorn"; "Walrus"; "Wolf";
                  "Cloud"; "Galaxy"; "Moon"; "Mountain"; "Prairie"; "Star"; "Valley"; "Wind"; "Rocket"|]
    [adjectives.[random.Next(Array.length adjectives)]; nouns.[random.Next(Array.length nouns)]; random.Next(1000).ToString()]
    |> List.reduce (+)

let handleFailure (r: HttpResponseMessage) =
    match r.IsSuccessStatusCode with
    | true -> r
    | false -> printfn "%s" (r.Content.ReadAsStringAsync () |> Async.AwaitTask |> Async.RunSynchronously)
               r.EnsureSuccessStatusCode()

let getContent (r: HttpResponseMessage) =
    r.Content.ReadAsStringAsync ()
    |> Async.AwaitTask
    |> Async.RunSynchronously

let createResourceGroup() =
    armClient.HttpInvoke (HttpMethod.Put, String.Format(resourceGroupCsmTemplate, defaultSubscription, defaultResourceGroupName, resourceGroupApiVersion) |> Uri, { name = defaultResourceGroupName; location = "west us"; })
    |> Async.AwaitTask
    |> Async.RunSynchronously
    |> handleFailure
    |> ignore

let createSite () = 
    createResourceGroup()
    let siteName = siteNameGenerator()
    armClient.HttpInvoke (HttpMethod.Put, String.Format(siteCsmTemplate, defaultSubscription, defaultResourceGroupName, siteName, websitesApiVersion) |> Uri, { location = "west us"; properties = { serverFarmId = defaultServerFarm } })
    |> Async.AwaitTask
    |> Async.RunSynchronously
    |> handleFailure
    |> ignore

    let scmUri = armClient.HttpInvoke (HttpMethod.Post, String.Format(sitePublishCredsCsmTemplate, defaultSubscription, defaultResourceGroupName, siteName, websitesApiVersion) |> Uri)
                  |> Async.AwaitTask
                  |> Async.RunSynchronously
                  |> (fun r -> r.EnsureSuccessStatusCode())
                  |> getContent
                  |> (fun v -> JsonConvert.DeserializeObject<PublishingCredential>(v))
                  |> (fun p -> p.properties.scmUri)
    printfn "%s" scmUri

let getAllSites () =
    armClient.HttpInvoke (HttpMethod.Get, String.Format(sitesCsmTemplate, defaultSubscription, defaultResourceGroupName, websitesApiVersion) |> Uri)
    |> Async.AwaitTask
    |> Async.RunSynchronously
    |> getContent
    |> (fun c -> JsonConvert.DeserializeObject<CsmMessageArray>(c))
    |> (fun a -> a.value)

let deleteSites () =
    getAllSites ()
    |> Array.map (fun s -> armClient.HttpInvoke (HttpMethod.Delete, String.Format(siteCsmTemplate, defaultSubscription, defaultResourceGroupName, s.name, websitesApiVersion) |> Uri))
    |> Array.map Async.AwaitTask
    |> Async.Parallel
    |> Async.RunSynchronously
    |> ignore
    printfn "done"

let listSites () =
    getAllSites ()
    |> Array.iter (fun s -> printfn "%s" s.name)

let run () =
    match getMode with
    | Some (mode) -> match mode with
                     | Create -> createSite ()
                     | Delete -> deleteSites ()
                     | List   -> listSites ()
    | None -> printfn "print help"

run()