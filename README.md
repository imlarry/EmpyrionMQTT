# ESB - Empyrion Service Bus

#### ESB is a messaging bus that implements the publish/subscribe pattern for use with Empyrion - Galactic Survival, by Eleon Game Studios.
<br>

> Copyright &copy; 2023 L.Goodhind
>
> This program is free software: you can redistribute it and/or 
modify it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or (at your 
option) any later version.
> 
> This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.
>
> You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.


### <p style="text-align: center;font-style: italic">* * * WORK IN PROGRESS * * *</p>

This is a *framework* for mod development using a publish/subscribe model to allow the 
execution of code outside the context of the game. While the code here is all C# services
can be developed and deployed in any language and across any OS that supports MQTT.

The current rev is not all that out-of-the-box useful as a player or game admin mod. My pace 
is **plodding** and I am in the crawl before you walk phase. If you see things worth doing 
that you know how to do please feel free to jump in with a pull request or fork as you see fit.

For a good open source broker it's really hard to beat [Eclipse Mosquitto](https://mosquitto.org/). Any examples 
here assume you're using it but any MQTT compliant broker should work.

***
## Getting Started:

1. You'll need a local MQTT broker, assuming it's [Mosquitto](https://mosquitto.org/) and it's directory
has been included on your path, start by verifying that the message transport is working. Open two command 
windows and in one enter the command `mosquitto_sub -v -t "#"` which is a request for all topics published 
to this broker to come to this window along with their message payloads.

1. In the second window enter the command `mosquitto_pub -t "Hello" -m "HelloWorld"` and if
everything is working the message "HelloWorld" with the topic "Hello" should appear in the first window. 
If it doesn't, you cannot go to space yet- figure out what's wrong and fix it.

1. Once that's working then create an /ESB directory under the Content/Mods folder in the
game and/or dedicated server directory. 

1. Build the application and copy ESB.dll, ESB_Info.yaml, MQTTnet.dll, Newtonsoft.Json.dll and YamlDotNet.dll into the /ESB directory.

1. Create a /Plugins directory as a child of /ESB and copy ESB.ModApi.dll here.

1. If you open the game and enter an existing save you should see a whole lot of messages
in the subscription window and their arrival will coincide with stuff you're doing in
the game. These events can be sent to multiple subscribing services and the game only 
needed to publish them once.

## What does it do?

When deployed in either the Empyrion - Galactic Survival/Content/Mods folder or the
Empyrion - Dedicated Server/Content/Mods folder, along with a properly configured ESB_Info.yaml
file, the mod connects with a message broker, establishes event handler callbacks, and then
subscribes to messages on topics that potentially interest it as defined in the info file. 

These topics and JSON structured data associated with them create a text driven API you can 
externally request information from or, to the extent allowed by the underlying mod APIs
and other service process, use to modify data and behavior in an active instance of the 
game. This combination allows external logic to orchestrate changes involving data from multiple 
publishers.

By combining data from different sources in this abstract view, the publish/subscribe
pattern enables loosely coupled communication between components or services. This allows 
different systems to communicate with each other and share data without needing to know 
about each other's implementation details. Because the communication is asynchronous, it 
can handle fairly high message volumes without impacting the performance of game service 
processes, especially if the broker is on a machine dedicated to the broker task.

The current implementation avoids security concerns by intentionally assuming a localhost 
broker; everything runs on one computer. This limits functionality to a "client/service"
model where a local instance of a client (or potentially a dedicated server) exchanges
messages with one or more service programs that are running on the same machine. While
MQTT supports network interconnection with robust security, the complexities of setup and
configuration for such a topology is currently out of scope.

       MP game client process interaction outside localhost/lan connections to 
       either shared external, dedi/client, or client/client requires careful 
       consideration to avoid unintended security and performance impacts.


## What is MQTT?

MQTT was originally "MQ series Telemetry Transport" or "Message Queuing Telemetry Transport", and is a protocol 
originally use in the gas industry for monitoring pipelines. It is designed as an ultralight protocol with limited
store/forward capabilites for communication with intermittant connected devices.

Today it is widely used for distributed system logging and orchestration as well as edge computing interfaces with
Internet of Things sorts of devices; if you've always wanted a light to blink in the real world when
something happens in the game, this is actually pretty straight forward to implement.

## Current Topic Format

MQTT uses a topic string to identify a resource (intentionally loose term) a client wants to 
access or provide. A standard URI structured "slash path" gives a good way to represent a
topic hierarchy so many systems employ some variation of one.

Since topic structure and naming conventions impact how this implementation determines who 
gets what messages and what code gets run by it, following a fairly rigid topic definition 
format ends up being critical.

 example:    ESB/Client/ModApi.GameEvent/E/20E9AD182A2D

             ESB/     identifies this as an Empyrion Service Bus message
      <sourceid>/     the type/class of the service ... basically an APPID
     <subjectid>/     the subject of the message (user topic string as dot path)
      <msgclass>/     Q=question/quest, R=response, E=event, I=information, X=exception 
      <clientid>      a unique session identifier (currently the last 12 of a guid)

A client subscribes to Questions/Quests that it can issue a Response to that provide information
or perform an action (a quest) and then confirms the request was satisfied. Events are issued as a result of async
events in the publisher. When errors occur eXceptions are raised.

The ESB uses the Application.Mode (Client, DedicatedServer, PlayfieldServer) as the sourceid 
for these services.

## Plugins

The basic messaging library supports loading dynamic linked libraries (DLLs) as plugins. This lets people easily
add routines needed by a service without modifying the basic framework. The ESB.ModApi.dll is an example of such a plugin
and implements a number of the basic api calls which can be triggered by sending JSON messages 
using `mosquitto_pub -t "<topicString>" -m "<JSON payload>"` calls from the command line. 

One of these, the Playfields.MoveEntity handler, is a happy path example that parses
the JSON message and uses the passed parameters to moves an entity in a playfield. If sent to a client mod it can 
move entities in the clients playfield. 

> mosquitto_pub -t "ESB/Client/ModApi.Playfield.MoveEntity/Q" -m "{ \\"EntityId\\": \\"1056\\", \\"Pos\\": \\"366.8, 31.7, -1085.6 \\" }"

In a dedicated game this will result in the entity rubberbanding back right
away when the playfield server that manages the entity detects the unauthorized movement. If sent to playfield servers
the server that has the entity loaded will move it and it will stay where it was positioned.

> mosquitto_pub -t "ESB/PlayfieldServer/ModApi.Playfield.MoveEntity/Q" -m "{ \\"EntityId\\": \\"2037\\", \\"Pos\\": \\"225.00, 59.0,782.6\\" }"

## Example Services:

Two examples of external console applications that interface with the service bus are included:

- data logger (Console.Datalog) that uses SQLite to log Application.GameEvent and Playfield.EntityLoaded messages.
- sound manager (Console.SoundMan) uses the NAudio lib to play an irritating noise when you open a constructor.