
SET PATH=%~dp0\koekatamarin_server\nodejs;%~dp0\koekatamarin_server\nodejs\node_modules
SET NODE_PATH=%~dp0\koekatamarin_server\nodejs\node_modules\


cd /d %~dp0\koekatamarin_server\server_en

node server.js
