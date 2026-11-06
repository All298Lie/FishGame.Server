CREATE DATABASE fish_game;

USE fish_game;

CREATE TABLE users (
    uid INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    nickname VARCHAR(20) NOT NULL UNIQUE,
    id VARCHAR(20) NOT NULL UNIQUE,
    pw VARCHAR(20) NOT NULL UNIQUE
);

CREATE TABLE game_histories (
    gid INT NOT NULL PRIMARY KEY,
    uid INT NOT NULL,
    score INT NOT NULL DEFAULT 0,
    FOREIGN KEY (`uid`) REFERENCES `users` (`uid`)
);

DESC game_histories