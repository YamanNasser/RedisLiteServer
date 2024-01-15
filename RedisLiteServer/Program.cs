using RedisLiteServer;

var redisServer = new RedisServer();
redisServer.LoadDatabaseState();
await redisServer.StartAsync();