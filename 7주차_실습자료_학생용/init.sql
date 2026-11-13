CREATE DATABASE fish_game;

USE fish_game;

CREATE TABLE users (
    uid INT NOT NULL AUTO_INCREMENT,
    nickname VARCHAR(20) NOT NULL UNIQUE,
    id VARCHAR(20) NOT NULL UNIQUE,
    pw VARCHAR(20) NOT NULL UNIQUE,

    PRIMARY KEY (`uid`)
);

CREATE TABLE game_histories (
    gid INT NOT NULL AUTO_INCREMENT,
    uid INT NOT NULL,
    score INT NOT NULL DEFAULT 0,
    
    PRIMARY KEY (`gid`, `uid`),
    FOREIGN KEY (`uid`) REFERENCES `users` (`uid`) ON DELETE CASCADE
);

DESC game_histories