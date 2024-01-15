# RedisLiteServer

## Introduction

RedisLiteServer is a lightweight C# implementation inspired by the challenge presented in "Write your own Redis Server" by John Crickett, available at [codingchallenges.fyi](https://codingchallenges.fyi/challenges/challenge-redis). This project aims to provide a simplified Redis-like server with fundamental functionalities.
It provides a simple key-value store with support for various commands such as SET, GET, EXISTS, DEL, INCR, DECR, LPUSH, RPUSH, SAVE, and LOAD.

This project is intended to serve as a learning resource for understanding the fundamentals of building a basic Redis server and is not suitable for production use.

## Project Components

### 1. RedisServer

The `RedisServer` class is the core component responsible for handling client connections, processing commands, and managing the key-value store. It uses TCP for communication and allows clients to interact with the server using a simplified Redis-like protocol.

### 2. KeyValueStore

The `KeyValueStore` class represents the in-memory key-value store used by the Redis server. It supports basic operations such as SET, GET, EXISTS, DEL, INCR, DECR, LPUSH, and RPUSH. Additionally, it handles key expiration based on specified options.

### 3. Serializer

The `Serializer` namespace contains classes for serializing and deserializing data. The `RespSerializer` class handles the Redis Serialization Protocol (RESP) format, and the `GeneralSerializer` class extends it to provide additional functionality for serializing and deserializing various data types.

## How to Install and Connect

To use the RedisLiteServer solution:

1. Clone the repository: `git clone https://github.com/YamanNasser/RedisLiteServer.git`
2. Open the solution in Visual Studio or your preferred C# development environment.
3. Build the solution.

To connect to the RedisLiteServer:

1. Start the RedisLiteServer by running the application.
2. Connect to the server using a Redis client library or a Redis CLI.
3. By default, the server listens on `127.0.0.1` and port `6379`.

Example using Redis CLI:

```redis-cli -h 127.0.0.1 -p 6379```

## Demo
Here are examples demonstrating the usage of various commands:

### SET and GET
```
# Set a key
SET myKey "Hello, RedisLiteServer!"

# Get the value for the key
GET myKey
```
### INCR and DECR
```
# Set an integer value
SET counter 10

# Increment the counter
INCR counter

# Decrement the counter
DECR counter
```
### LPUSH and RPUSH
```
# Left push (prepend) items to a list
LPUSH myList "item1" "item2" "item3"

# Right push (append) items to a list
RPUSH myList "item4" "item5"
```
### EXISTS and DEL
```
# Check if a key exists
EXISTS myKey

# Delete a key
DEL myKey
```
### SAVE and LOAD
```
# Save the current database state to a file
SAVE

# Load the database state from a file
LOAD
```

# Contributing
Feel free to explore additional Redis commands and experiment with the provided commands to understand the RedisLiteServer functionalities.

# Connect with me on LinkedIn
 <a href="https://www.linkedin.com/in/yamannasser/">Yaman Nasser</a> Software Engineer

# Support
Has this Project helped you learn something New? or Helped you at work? Do Consider Supporting. Here are a few ways by which you can support.
1. Leave a star! ⭐
2. Recommend this awesome project to your colleagues.
