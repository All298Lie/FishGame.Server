-- ════════════════════════════════════════════════════════════
--  9주차 실험 — 가짜 플레이어 10만 명 넣기 (root 로 실행)
--
--  "내 순위" 를 MySQL 과 Redis 로 구해서 속도를 비교할 때 씁니다.
--  닉네임은 bot000001 ~ bot100000, 점수는 0 ~ 9999 사이 무작위.
--  끝나면 03_bench_clean.sql 로 지웁니다.
-- ════════════════════════════════════════════════════════════
USE battlearena_game;

-- 재귀 CTE 는 기본 1000단계까지라 늘려 줍니다 (이 접속에서만)
SET SESSION cte_max_recursion_depth = 200000;

INSERT INTO players (nickname, best_score, total_score, games_played)
WITH RECURSIVE seq (n) AS (
    SELECT 1
    UNION ALL
    SELECT n + 1 FROM seq WHERE n < 100000
)
SELECT CONCAT('bot', LPAD(n, 6, '0')),
       FLOOR(RAND(n) * 10000),
       0,
       1
  FROM seq;

SELECT COUNT(*) AS players FROM players;
