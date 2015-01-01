Winterleaf.ProxyServer
======================

This is a Simple web proxy which works like fiddler.

It is not meant to be a network proxy but instead a local proxy.

What makes this proxy special is that you can spoof the referer 
of any request by appending &x123Referer=<Some Host> at the end of
any URL and the proxy server will make the request's referer be that host.

This is useful when building plugins for PlayOn media center since 
their is currently no way to spoof the referer in a plugin.

Vince