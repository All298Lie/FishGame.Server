-- ════════════════════════════════════════════════════════════
--  9주차 실험 정리 — 가짜 플레이어 지우기 (root 로 실행)
--
--  ba_server 에는 DELETE 권한이 없어서 root 로 지웁니다. (8주차 최소 권한)
--  가짜 플레이어는 판 기록(match_results)이 없어서 외래키에 걸리지 않습니다.
-- ════════════════════════════════════════════════════════════
USE battlearena_game;

SET SQL_SAFE_UPDATES = 0;   -- WHERE 가 기본키가 아니라서 안전 모드를 잠시 끕니다 (7주차)
DELETE FROM players WHERE nickname REGEXP '^bot[0-9]{6}$';
SET SQL_SAFE_UPDATES = 1;

SELECT COUNT(*) AS players FROM players;
