# CSharp-RelayServer
Relay server to translate TCP packets to SSL, wait for a response, then translate it back to the originating TCP socket

A client had an issue where their operating system did not support TLS1.2 communications. One of their clients now requires TLS1.2, and upgrading the operating system of their server wasn't an option in the forseeable future.

To resolve this issue, I created a relay server that listens for TCP packets from their server, translates them to TLS1.2 packets, submits them to the client, awaits the response, then translates it back to TCP to return to their server.

Listeners is asynchronous.
