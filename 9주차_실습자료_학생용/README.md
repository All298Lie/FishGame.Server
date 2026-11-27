# 9주차 실습 — TICKET-051 랭킹이 실시간으로

8주차에 MySQL 에 저장한 판 결과로 **Redis 실시간 랭킹**을 만듭니다.
이 폴더의 8주차 부분(DB 저장)은 완성본입니다. 8주차를 다 못 했어도 여기서 시작하면 됩니다.

## 준비

1. MSOpenTech Redis 3.0.504 를 설치합니다 (msi — Windows 서비스로 포트 6379 에서 켜짐)
   <https://github.com/microsoftarchive/redis/releases>
2. Workbench 에서 **root** 로 `Database/00_setup.sql` 실행
3. `BattleArenaServer/dbsettings.sample.json` 을 복사해 `dbsettings.json` 으로 만들고 비밀번호 칸 채우기
   (`"Redis": "127.0.0.1:6379"` 줄도 있어야 합니다)
4. `BattleArena.sln` 열기 → NuGet 복원
5. **RedisLab**(예제 R) 실행 → 1번에서 `연결됨 : True`

## 할 일 — TODO 7개

| # | 파일 | 내용 | 막히면 |
|---|---|---|---|
| 1 | `Cache/RedisCache.cs` | Redis 연결 · PING | 예제 R 1번 |
| 2 | `Cache/RankingCache.cs` | 최고 점수 갱신 (`ZSCORE` → 비교 → `ZADD`) | 예제 R 6번 |
| 3 | 〃 | TOP N (`ZREVRANGE`) | 예제 R 5번 |
| 4 | 〃 | 내 순위 (`ZREVRANK`) | 예제 R 5번 · 7번 |
| 5 | 〃 | MySQL 에서 다시 채우기 | 예제 R 8번 앞부분 |
| 6 | `Db/SaveQueue.cs` | 판 저장 뒤 랭킹 반영 + 본인에게 알림 | 주석 |
| 7 | `ClientSession.cs` | 로그인 인사에 전체 순위 | TODO 4 |

순서 : 1 → 2·3·4 → 5 → 6 → 7. 비워 둔 상태로도 빌드 · 실행됩니다 (랭킹은 MySQL 로 대신).

## 확인

- 서버 창 `rank` (Redis) 와 `rankdb` (MySQL) 결과가 같은지
- 판이 끝나면 TestClient 화면에 `랭킹 : 전체 N위 (최고 N점)`
- 다시 접속하면 인사에 `· 전체 N위`
- `redis-cli FLUSHALL` 로 Redis 를 비운 뒤 서버 창 `rebuild` 로 랭킹이 돌아오는지

## 제출

eClass 에 스크린샷, 코드는 GitHub 에 커밋 (`dbsettings.json` 은 올라가지 않는 게 정상).
