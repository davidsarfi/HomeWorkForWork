# HomeWorkForWork
This is a qa homework to create automation tests for the service in the code.
Test plans are written for each test test types can be found in the corresponding test folder.

### Build Docker image

Run this command from the directory where there is the solution file.

```
docker build -f src/Betsson.OnlineWallets.Web/Dockerfile .
```

### Run Docker container

```
docker run -p <port>:8080 <image id>
```

### Open Swagger

```
http://localhost:<port>/swagger/index.html
```
