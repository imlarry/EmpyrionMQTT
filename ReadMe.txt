ESB is a messaging bus that implements the publish/subscribe pattern for use with
Empyrion - Galactic Survival, by Eleon Game Studios.

    Copyright (C) 2023 L.Goodhind

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.

                                   * * *

Getting Started:

1. You'll need a local MQTT broker, assuming it's Mosquitto and it's on your path start
by verifying that the message transport is working. Open two command windows and in one
type "mosquitto_sub -v -t "#" ... this is a request for all topics published to come to
this window along with their message payloads.

2. In the second window enter the command mosquitto_pub -t "Hello" -m "HelloWorld" .. if
everything is working the message should show up in the first window. If it isn't should
not go to space yet- figure out what's wrong and fix it.

3. Once that's working then create an /ESB directory under the Content/Mods folder in the
game directory. Put the ESB dll, the associated _Info.yaml file, and the MQTTnet, 
Newtonsoft.Json and YamlDotNet dll files in as well.

4. If you open the game and enter an existing save you should see a whole lot of messages
in the subscription window and their arrival will coincide with stuff you're doing in
the game. These events can be used to drive multiple subscribing services and the game only 
needed to publish them once.

Example Services:

The two examples are a data logger and the start of a sound manager using the NAudio lib
that plays an irritating noise when you open the constructor.

                                   * * *

What does it do?

When deployed either the Empyrion - Galactic Survival/Content/Mods folder or the
Empyrion - Dedicated Server/Content/Mods folder, along with a properly configured ESB_Info.yaml
file, the mod connects with a message broker, establishes event handler callbacks, and then
subscribes to messages on topics that potentially interest it. 

These topics and structured data associated with them create a simple message API you can 
externally request information from or, to the extent allowed by the underlying mod APIs
and other service process, use to modify data and behavior in an active instance of the 
game. This allows external logic to orchestrate changes involving data from multiple 
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
messages with one or more service programs that are running on the same machine.

       MP game client process interaction outside localhost/lan connections to 
       either shared external, dedi/client, or client/client requires careful 
       consideration to avoid unintended security and performance impacts.

For a good open source broker it's really hard to beat Eclipse Mosquitto. Any examples 
will assume you're using it but anything MQTT compliant should work. https://mosquitto.org/

MQTT was originally "MQ series Telemetry Transport" or "Message Queuing Telemetry Transport"
and is a protocol originally use in the gas industry for monitoring pipelines and was designed
as an ultralight protocol for store/forward communication with intermittant connected devices.

