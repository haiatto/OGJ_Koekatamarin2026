
SET PATH=%~dp0koekatamarin_server\nodejs;%~dp0koekatamarin_server\nodejs\node_modules
SET NODE_PATH=%~dp0koekatamarin_server\nodejs\node_modules\


cd /d %~dp0koekatamarin_server\server_ja

node server.js
