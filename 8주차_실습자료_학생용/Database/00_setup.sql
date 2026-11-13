-- ════════════════════════════════════════════════════════════
--  8주차 — 게임 서버용 DB 준비
--
--  Workbench 에서 root 로 접속한 뒤, 이 파일을 처음부터 끝까지 실행하세요.
--  몇 번을 다시 실행해도 처음 상태로 돌아갑니다.
--
--  만드는 것
--    1. 데이터베이스 battlearena_game
--    2. 테이블 3개 : players, matches, match_results
--    3. 샘플 데이터 (랭킹이 비어 보이지 않게)
--    4. 게임 서버 전용 계정 ba_server   ← root 를 쓰지 않습니다
-- ════════════════════════════════════════════════════════════

DROP DATABASE IF EXISTS battlearena_game;
CREATE DATABASE battlearena_game;
USE battlearena_game;


-- ── 1. 플레이어 ─────────────────────────────────────────────
--   best_score · total_score · games_played 는 랭킹 화면용으로 일부러 저장합니다.
--   판 결과를 저장할 때 같은 트랜잭션에서 함께 고칩니다.
CREATE TABLE players (
    id             INT          AUTO_INCREMENT PRIMARY KEY,
    nickname       VARCHAR(20)  NOT NULL UNIQUE,
    best_score     INT          NOT NULL DEFAULT 0,
    total_score    BIGINT       NOT NULL DEFAULT 0,
    games_played   INT          NOT NULL DEFAULT 0,
    created_at     DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_login_at  DATETIME     NULL,
    INDEX idx_best_score (best_score)
);


-- ── 2. 판 ───────────────────────────────────────────────────
CREATE TABLE matches (
    id          BIGINT    AUTO_INCREMENT PRIMARY KEY,
    started_at  DATETIME  NOT NULL,
    ended_at    DATETIME  NULL
);


-- ── 3. 판 결과 (players ↔ matches 의 N:M 연결) ──────────────
CREATE TABLE match_results (
    match_id   BIGINT  NOT NULL,
    player_id  INT     NOT NULL,
    score      INT     NOT NULL,
    ranking    INT     NOT NULL,
    coins      INT     NOT NULL DEFAULT 0,
    gems       INT     NOT NULL DEFAULT 0,
    PRIMARY KEY (match_id, player_id),
    FOREIGN KEY (match_id)  REFERENCES matches(id),
    FOREIGN KEY (player_id) REFERENCES players(id)
);


-- ── 4. 샘플 데이터 ─────────────────────────────────────────
--   last_login_at 이 NULL 인 사람(박민수)은 가입만 하고 한 번도 안 들어온 사람입니다.
INSERT INTO players (nickname, best_score, total_score, games_played, created_at, last_login_at) VALUES
    ('홍길동', 23, 39, 2, '2026-11-01 19:00:00', '2026-11-08 20:00:00'),
    ('김철수', 12, 18, 2, '2026-11-01 19:05:00', '2026-11-08 20:01:00'),
    ('이영희', 21, 21, 1, '2026-11-02 10:00:00', '2026-11-08 20:02:00'),
    ('박민수',  0,  0, 0, '2026-11-04 22:00:00', NULL),
    ('최강자', 30, 30, 1, '2026-11-05 21:00:00', '2026-11-08 21:00:00');

INSERT INTO matches (started_at, ended_at) VALUES
    ('2026-11-08 20:05:00', '2026-11-08 20:06:00'),
    ('2026-11-08 20:10:00', '2026-11-08 20:11:00'),
    ('2026-11-08 21:05:00', '2026-11-08 21:06:00');

INSERT INTO match_results (match_id, player_id, score, ranking, coins, gems) VALUES
    (1, 1, 16, 1, 6, 2),
    (1, 2, 12, 2, 7, 1),
    (2, 1, 23, 1, 8, 3),
    (2, 3, 21, 2, 6, 3),
    (2, 2,  6, 3, 6, 0),
    (3, 5, 30, 1, 5, 5);


-- ── 5. 게임 서버 전용 계정 ─────────────────────────────────
--   게임 서버는 root 로 접속하지 않습니다.
--   필요한 권한만 줍니다 : 읽기(SELECT) · 넣기(INSERT) · 고치기(UPDATE)
--   DELETE · DROP 은 주지 않습니다.
--   → 서버 코드가 뚫려도 테이블을 지우거나 데이터를 삭제할 수 없습니다.
DROP USER IF EXISTS 'ba_server'@'localhost';
CREATE USER 'ba_server'@'localhost' IDENTIFIED BY 'ba-server-2026';
GRANT SELECT, INSERT, UPDATE ON battlearena_game.* TO 'ba_server'@'localhost';

-- 확인
SHOW GRANTS FOR 'ba_server'@'localhost';
SELECT nickname, best_score FROM players ORDER BY best_score DESC;
