# SUDPP - Simple UDP Protocol

SUDPP is simple protocol based on UDP. It's very simple and has low latency. With this protocol it is possible to send packets and automatically confirm fact of delivery. I can recommend it for creating simple online games or other real time programs. It was was created for my university laboratory task.

**Features:**
1. Simple - only 3 small files, easy integration.
2. Stable.
3. Fast - based on UDP, with as small amounts of headers as possible.
4. It's possible to confirm packets from client side (Acknowledgement packets).
5. The same library for client and server. No need of modyfing code.

Simple demo (Server and Client) is included. It's colaborative drawing application. You can see how absic communication between server and client is done here.