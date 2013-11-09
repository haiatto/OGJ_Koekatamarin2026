var http     = require('http');
var WSServer = require('websocket').server;

var url        = require('url');
var clientHtml = require('fs').readFileSync('client.html');

var plainHttpServer = http.createServer(function(req, res) {
	res.writeHead(200, { 'Content-Type': 'text/html'});
	res.end(clientHtml);
}).listen(8888);

var webSocketServer = new WSServer({httpServer: plainHttpServer});
var accept          = ['localhost', '127.0.0.1'];

var connections=[]

webSocketServer.on('request', function (req) {
	req.origin = req.origin || '*';
//	if (accept.indexOf(url.parse(req.origin).hostname) === -1) 
//	{
//		req.reject();
//		console.log(req.origin + ' access not allowed.');
//		return;
//	}

    // ここでコネクションが確立される
	var websocket = req.accept(null, req.origin);

	websocket.on('message', function(msg) {
		console.log('"' + msg.utf8Data + '" is recieved from ' + req.origin + '!');
		if (msg.utf8Data === 'Hello') {
			websocket.send('sended from WebSocket Server');
		}
		else{
		    websocket.send('sended from WebSocket Server' + msg.utf8Data);
		}
		// 全クライアントに配信
		for(var ii=0; ii < connections.length; ii++)
		{
		    connections[ii].send(msg.utf8Data);
		}
	});

	websocket.on('close', function (code,desc) {
		console.log('connection released! :' + code + ' - ' + desc);
		
		connections = connections.filter(function(v){
		  return v != websocket;
		});
	});
	
	connections.push(websocket);
});
