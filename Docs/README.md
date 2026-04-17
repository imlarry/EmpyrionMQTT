# ESB - Empyrion Service Bus

#### ESB is a messaging bus that implements the publish/subscribe pattern for use with Empyrion - Galactic Survival, by Eleon Game Studios.
<br>

> Copyright &copy; 2023 L.Goodhind
>
> This project is licensed under the MIT License. See the LICENSE file in the repository root for the full license text.


### <p style="text-align: center;font-style: italic">* * * WORK IN PROGRESS * * *</p>

This is a *framework* for mod development using a publish/subscribe model to allow the execution of code outside the context of the game. While the code here is all C#, service clientscan be developed and deployed in any language and across any OS that supports MQTT.

The current rev is not all that out-of-the-box useful as a player or game admin mod. My pace is **plodding** and I am in the crawl before you walk phase. If you see things worth doing that you know how to do please feel free to jump in with a pull request or fork as you see fit.

For a good open source broker it's really hard to beat [Eclipse Mosquitto](https://mosquitto.org/). Any examples 
here assume you're using it but any MQTT compliant broker should work.

***
## Getting Started:

1. You'll need a local MQTT broker, assuming it's [Mosquitto](https://mosquitto.org/) and it's directory has been included on your path, start by verifying that the message transport is working. Open two command windows and in one enter the command `mosquitto_sub -v -t "#"` which is a request for all topics published to this broker to come to this window along with their message payloads.

1. In the second window enter the command `mosquitto_pub -t "Hello" -m "HelloWorld"` and if everything is working the message "HelloWorld" with the topic "Hello" should appear in the first window. If it doesn't, you cannot go to space yet- figure out what's wrong and fix it.

1. Once that's working, create an /ESB directory under the Content/Mods folder in the game and/or dedicated server directory. 

1. Build the application and copy ESB.dll, along with all the other dlls in the bin directory, to the /ESB directory.

1. If you open the game and enter an existing save you should see a whole lot of messages in the subscription window and their arrival will coincide with stuff you're doing in the game. These events can be sent to multiple subscribing client services and the game only needed to publish them once ... this one-to-many distribution is the core of the publish/subscribe pattern.

## What does it do?

When deployed in either the Empyrion - Galactic Survival/Content/Mods folder or the Empyrion - Dedicated Server/Content/Mods folder, along with a properly configured ESB_Info.yaml file, the mod connects with a message broker, establishes event handler callbacks, and then subscribes to messages on topics that potentially interest it as defined in the info file. 

These topics and JSON structured data associated with them create a text driven API you can externally request information from or, to the extent allowed by the underlying mod APIs and other service process, use to modify data and behavior in an active instance of the game. 

By combining data from different sources in this abstract view, the publish/subscribe pattern enables loosely coupled communication between components or services. This allows different systems to communicate with each other and share data without needing to know about each other's implementation details. Because the communication is asynchronous, it can handle fairly high message volumes without impacting the performance of game service processes, especially if the broker is on a machine dedicated to the broker task.

The current implementation avoids security concerns by intentionally assuming a localhost broker; everything runs on one computer. This limits functionality to a "client/service" model where a local instance of a client (or potentially a dedicated server) exchanges messages with one or more service programs that are running on the same machine. While MQTT supports network interconnection with robust security, the complexities of setup and configuration for such a topology is currently out of scope.

       MP game client process interaction outside localhost/lan connections to 
       either shared external, dedi/client, or client/client requires careful 
       consideration to avoid unintended security and performance impacts.


## What is MQTT?

MQTT was originally "MQ series Telemetry Transport" or "Message Queuing Telemetry Transport", and is a protocol originally use in the gas industry for monitoring pipelines. It is a lightweight protocol with limited store/forward capabilites for communication with potentially intermittantly connected devices.

Today it is widely used for distributed system logging and orchestration as well as edge computing interfaces with Internet of Things sorts of devices; if you've always wanted a light to blink in the real world when something happens in the game, this is actually pretty straight forward to implement.

## Current Topic Format

Topics follow a five-segment slash path: `{appId}/{msgClass}/{subject}/{clientId}/{seq}`. Message classes are `Q` (request), `R` (response), `E` (event), `I` (information), `X` (exception). The ESB uses `Application.Mode` (`Client`, `DedicatedServer`, `PlayfieldServer`) as the `appId`.

See [TopicFormat.md](TopicFormat.md) for the full addressing spec including multicast conventions and external client correlation patterns.

## Topic Handlers

The basic messaging library supports compile-time linked libraries (DLLs) or shared public classes in the core project as topic handlers. This requires building new topic handlers to add routines needed by a service to modify the basic framework. Refer to the files in /ESB/TopicHandlers for examples of requesting game API calls by entering `mosquitto_pub -t "<topicString>" -m "<JSON payload>"` from the command line. 

While it would be possible to add topic handlers at runtime, this would expose the user to potential security issues from untrusted code. For the time being this mechanism has been removed.
