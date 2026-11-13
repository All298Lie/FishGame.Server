-- ════════════════════════════════════════════════════════════
--  8주차 — 서버가 제대로 저장했는지 확인하는 쿼리
--
--  서버를 켜고 판을 한두 번 돌린 뒤, 한 문장씩 실행하세요.
-- ════════════════════════════════════════════════════════════
USE battlearena_game;

-- 1. 최근 판 5개
SELECT id, started_at, ended_at
  FROM matches
 ORDER BY id DESC
 LIMIT 5;

-- 2. 가장 최근 판의 결과
SELECT r.ranking, p.nickname, r.score, r.coins, r.gems
  FROM match_results r
  JOIN players p ON p.id = r.player_id
 WHERE r.match_id = (SELECT MAX(id) FROM matches)
 ORDER BY r.ranking;

-- 3. 랭킹 TOP 10 (서버의 /rank 와 같은 쿼리)
SELECT nickname, best_score, games_played
  FROM players
 ORDER BY best_score DESC, id
 LIMIT 10;

-- 4. 검산 — 저장해 둔 통계와 판 기록이 같은가?
--    트랜잭션이 제대로 되었다면 diff 가 전부 0 입니다.
SELECT p.nickname,
       p.total_score,
       COALESCE(SUM(r.score), 0)             AS sum_of_matches,
       p.total_score - COALESCE(SUM(r.score), 0) AS diff,
       p.games_played,
       COUNT(r.match_id)                     AS matches_counted
  FROM players p
  LEFT JOIN match_results r ON r.player_id = p.id
 GROUP BY p.id, p.nickname, p.total_score, p.games_played;

-- 5. 결과 없는 판이 있는가? (트랜잭션이 깨졌다면 여기 나옵니다)
SELECT m.id
  FROM matches m
  LEFT JOIN match_results r ON r.match_id = m.id
 WHERE r.match_id IS NULL;

-- 6. 최근 로그인
SELECT nickname, last_login_at
  FROM players
 ORDER BY last_login_at DESC
 LIMIT 5;
