# 8주차 실습 — 판 결과가 DB 에 남게 하라 (TICKET-041 후반전)

7주차에 Workbench 로 쳤던 SQL 을 **C# 코드로 옮겨서** 게임 서버가 직접 MySQL 을 쓰게 만듭니다.

---

## 0. 준비 (순서대로)

1. **DB 만들기** — Workbench 에 root 로 접속해서 `Database/00_setup.sql` 을 처음부터 끝까지 실행
   - DB `battlearena_game`, 테이블 3개, 샘플 플레이어 5명, 게임 서버 전용 계정 `ba_server` 가 만들어집니다
   - 몇 번을 다시 실행해도 처음 상태로 돌아갑니다
2. **연결 문자열 파일 만들기**
   - `BattleArenaServer/dbsettings.sample.json` 을 복사 → 이름을 `dbsettings.json` 으로
   - `여기에_비밀번호` 를 `ba-server-2026` 으로 바꾸기
   - ★ `dbsettings.json` 은 `.gitignore` 에 들어 있어서 GitHub 에 올라가지 않습니다. 그게 정상입니다.
3. **Visual Studio 에서 `BattleArena.sln` 열기** → NuGet 패키지가 자동으로 받아집니다 (MySqlConnector)
4. **SqlLab 실행 → 1번** — `ba_server@localhost` 가 나오면 준비 끝

---

## 1. 예제 I — SqlLab (읽기용 완성본)

SQL 을 C# 으로 옮기는 방법을 9단계로 보여 줍니다. 메뉴를 하나씩 실행하면서 코드를 읽으세요.
**TODO 를 하다 막히면 같은 모양의 SqlLab 단계를 보세요.**

| 메뉴 | 배우는 것 | 이걸 보면 되는 TODO |
|---|---|---|
| 1 | 연결 · 값 하나 (`ExecuteScalarAsync`) | 1 |
| 2 | 한 줄 읽기 (`ExecuteReaderAsync` · `GetInt32`) | 2 |
| 3 | 여러 줄 읽기 (`while ReadAsync`) | 8 |
| 4 | 집계 값의 C# 타입 (`long` · `decimal`) | — |
| 5 | 빈칸 채우기 (`@nick` 파라미터) | 2 |
| 6 | 넣기와 새 번호 (`LastInsertedId` · 1062) | 3, 4 |
| 7 | 고치기와 NULL (`ExecuteNonQueryAsync` · `IsDBNull`) | 4 |
| 8 | SQL 인젝션 (붙이기 vs 파라미터) | 2 |
| 9 | 트랜잭션 (`BeginTransactionAsync` · `Commit` · `Rollback`) | 5 |

> 8번에서 두 번째 예시를 넣으면 홍길동 점수가 9999 로 바뀝니다. `00_setup.sql` 을 다시 실행하면 돌아옵니다.

---

## 2. TODO 8개 — `BattleArenaServer`

TODO 를 비워 둔 상태로도 서버는 **빌드되고 실행됩니다.** DB 를 못 쓰면 손님으로 들어가서 게임은 계속됩니다.
1 → 2·3·4 → 5·6·7 순서로 채우세요.

| # | 파일 | 할 일 |
|---|---|---|
| 1 | `Db/Db.cs` | `SELECT VERSION()` 으로 연결 확인 |
| 2 | `Db/PlayerRepository.cs` | 닉네임으로 찾기 — SELECT 한 줄 → `PlayerRecord` |
| 3 | 〃 | 새 플레이어 — INSERT → 새 id |
| 4 | 〃 | 로그인 흐름 — 찾기 → 없으면 만들기 → 1062 처리 → 접속 시각 |
| 5 | `Db/MatchRepository.cs` | 판 저장 — 트랜잭션으로 3종류 SQL |
| 6 | `Db/SaveQueue.cs` | 저장 작업자 — 큐에서 꺼내 저장, 실패하면 재시도 |
| 7 | `GameLoop.cs` (`EndRound`) | 판 결과를 저장 큐에 넣기 (★ await 하지 않음) |
| 8 | `Db/RankingRepository.cs` | (보너스) 랭킹 TOP 10 |

### 단계마다 서버 로그가 이렇게 바뀝니다

| 채운 것 | 서버 로그 |
|---|---|
| 없음 | `[DB] TODO 1 이 아직 비어 있습니다` |
| 1 | `[DB] 연결 성공 — MySQL 8.4.x` |
| 1~4 | `[로그인] 홍길동 (접속번호 1 · DB id 1)` |
| 1~7 | `[판] 결과를 저장 큐에 넣었습니다` → `[저장] 3판 → matches.id 7` |

한 판이 60초라 기다리기 지루하면 실행 인자에 `--round 15` 를 넣으세요. (VS : 프로젝트 속성 → 디버그 → 명령줄 인수)

---

## 3. 확인

1. **서버 콘솔에 `rank`** — 랭킹 TOP 10 이 나오면 TODO 8 까지 된 것
2. **Workbench 에서 `Database/01_check.sql`** — 4번 검산의 `diff` 가 전부 0, 5번 "결과 없는 판" 이 0줄이어야 합니다
3. **CheatClient(예제 G) 7번** — "차단" 이 나와야 합니다. "뚫림" 이면 TODO 2 에서 닉네임을 SQL 에 붙인 것입니다

---

## 4. 제출

- 수정한 코드는 GitHub 에 커밋 (★ `dbsettings.json` 은 올라가지 않는 게 정상)
- eClass 에 스크린샷 3장 : 서버 로그의 `[저장]` 줄 · `01_check.sql` 검산 결과 · CheatClient 7번 "차단"
- 파일 이름은 영어·숫자만 : `학번_W8.zip` (예 : `23230881_W8.zip`)

문의 : dyu.game.network@gmail.com 또는 문자 010-8817-6611 (학번과 이름을 꼭 적어 주세요)
