-- 데이터베이스 확인 및 생성
CREATE DATABASE IF NOT EXISTS battlearena_game;

-- 데이터베이스 사용, 테이블 확인 및 생성
USE battlearena_game;

CREATE TABLE IF NOT EXISTS players (
    id INT UNSIGNED NOT NULL AUTO_INCREMENT,
    nickname VARCHAR(20) NOT NULL UNIQUE,
    best_score INT UNSIGNED NOT NULL DEFAULT 0,
    total_score BIGINT UNSIGNED NOT NULL DEFAULT 0,
    games_played INT UNSIGNED NOT NULL DEFAULT 0,
    create_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_login_at DATETIME NULL,

    PRIMARY KEY (`id`)
);

CREATE TABLE IF NOT EXISTS matches (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    started_at DATETIME NOT NULL,
    ended_at DATETIME NULL,

    PRIMARY KEY (`id`)
);

CREATE TABLE IF NOT EXISTS match_results (
    match_id BIGINT UNSIGNED NOT NULL,
    player_id INT UNSIGNED UNSIGNED NOT NULL,
    score INT UNSIGNED NOT NULL DEFAULT 0,
    ranking INT UNSIGNED NOT NULL,
    coins INT UNSIGNED NOT NULL DEFAULT 0,
    gems INT UNSIGNED NOT NULL DEFAULT 0,
    
    PRIMARY KEY (`match_id`, `player_id`),
    FOREIGN KEY (`match_id`) REFERENCES `matches` (`id`) ON DELETE CASCADE,
    FOREIGN KEY (`player_id`) REFERENCES `players` (`id`) ON DELETE CASCADE
);

-- 데이터베이스 유저 계정 확인 및 생성, 권한 부여
CREATE USER IF NOT EXISTS 'ba_server'@'localhost' IDENTIFIED BY 'ba-server-2026';
GRANT SELECT, INSERT, UPDATE ON battlearena_game.* TO 'ba_server'@'localhost';