# FishGame.Server
DYU 게임서버프로그래밍 학기작 서버

# 물고기 게임 - 1조

## 장르

- 캐주얼

## 인원

- 2~10명

## 규칙

- 3분 내에 300점을 달성하거나 가장 높은 점수를 유지하는 사람이 승리
- 점수가 낮은 플레이어의 캐릭터를 내 캐릭터로 30% 이상을 덮을 경우 해당 플레이어를 죽이고, 해당 플레이어의 점수 흡수

## 조작

- W,A,S,D - 이동

## 팀원 목록

- 윤태희
- 임준
- 조영래

## 게임에서 작동해야하는 것

- 게임을 켰을때 서버에 연결되야 한다.
- 매칭을 통해 최소 인원이 모이면 게임방이 생성되어 시작된다.
- 플레이하고 있는 유저의 방에 난입할 수 있어야 한다.
- 아이템을 먹을 경우, 캐릭터의 덩치가 커진다.
- 아이템을 먹을 경우, 다른 플레이어에게도 아이템이 사라져야한다.
- 아이템을 먹을 경우, 점수가 올라야 한다.
- 3분이 지나면 게임이 종료되어야 한다.
- 어느 플레이어가 300점에 도달할 경우 게임이 종료되어야 한다.
- 게임이 종료되면 다른 플레이어를 죽인 횟수, 죽은 횟수, 이때까지 아이템을 먹은 갯수, 죽기전까지 최대로 들고있던 점수 등을 전적으로 저장한다.
- 전적을 확인할 수 있어야한다.

## 서버가 해야할 일

<table align="center">
	<tr>
		<td align="center"><b>게임에서 벌어지는 일</td>
		<td align="center"><b>서버가 해야할 일</td>
		<td align="center"><b>관련 패킷</td>
	</tr>
	<tr>
		<td align="center">게임을 킨다</td>
		<td>1. 로그인 서버로 연결<br>
  2. 회원가입 혹은 로그인 시도하도록 유도<br>
  3. 회원가입을 할 경우, 회원 정보를 데이터베이스에 저장<br>
  4. 로그인을 할 경우, 회원정보를 데이터베이스에서 확인하고, 일치할경우 해당 정보를 토대로 게임서버로 연결</td>
		<td>C2S_LoginReq<br>
		C2S_ResisterReq<br>
		S2C_LoginRes<br>
		S2C_ResisterRes</td>
	</tr>
	<tr>
		<td align="center">매칭을 시작한다</td>
		<td>1. 게임서버에서 매칭을 시도하는 유저들 정보를 모은다.<br>
  2. 현재 생성된 방 중에 인원이 다 차지 않은 방이 있을 경우, 해당 방으로 유저를 난입시켜준다.<br>
  3. 현재 생성된 방 중에 인원이 다 찼을 경우, 매칭에 필요한 최소 인원이 모일떄까지 대기한다.<br>
  4. 매칭에 필요한 최소 인원이 모일 경우 새 방을 생성해서 유저를 이동시킨다.</td>
		<td>C2S_MatchMakingReq<br>
		S2C_MatchMakingRes<br>
		S2C_MatchNoti</td>
	</tr>
	<tr>
		<td align="center">난입 요청을 한다</td>
		<td>1. 게임서버에서 요청을 한 유저가 입력한 닉네임이 현재 온라인인지 확인한다.<br>
  2. 오프라인일 경우, 잘못된 유저 정보라고 전달하며 난입 요청을 취소한다.<br>
  3. 온라인일 경우, 해당 유저가 게임을 진행 중인지 확인한다.<br>
  4. 해당 유저가 게임을 진행중이지 않을 경우, 게임을 진행하고 있지 않다고 전달하며 난입 요청을 취소한다.<br>
  5. 게임을 진행 중일 경우, 현재 게임 방의 인원이 가득 찼는지 확인한다.<br>
  6. 게임 방 인원이  가득 찼을 경우, 인원이 찼다고 전달하며 난입 요청을 취소한다.<br>
  7. 게임 방 인원이 가득 차지 않았을 경우, 해당 방으로 난입을 진행한다.</td>
		<td>C2S_DropInReq<br>
		S2C_DropInRes</td>
	</tr>
	<tr>
		<td align="center">게임이 시작된다</td>
		<td>1. 초기 대기 시간 3초를 주어준다.<br>
  2. 대기시간이 끝나면 플레이어들이 움직일 수 있도록 조작을 풀어준다.<br>
  3. 제한시간 3분을 카운트 시작한다.</td>
		<td>S2C_GameStartNoti<br>
		S2C_GameEndNoti</td>
	</tr>
	<tr>
		<td align="center">캐릭터를 이동한다</td>
		<td>1. 유저에게 방향키 입력을 전송 받을 경우, 모든 유저(본인 포함)에게 해당 유저의 이동 사실을 알린다(udp)<br>
  2. “플레이어가 다른 플레이어에 닿는다”에서 계속…</td>
		<td>C2S_MoveReq<br>
		S2C_MoveRes<br>
		S2C_MoveNoti</td>
	</tr>
	<tr>
		<td align="center">아이템을 먹는다</td>
		<td>1. 유저에게 아이템을 먹었다는 내용을 받는다.<br>
  2. 해당 아이템을 더이상 먹지 못하도록 lock으로 잠군다<br>
  3. 먹은 플레이어의 점수를 올리고, 모든 유저에게 알린다<br>
  4. 먹은 플레이어의 캐릭터 덩치를 조정하고, 모든 유저에게 알린다(tcp)<br>
  5. 해당 아이템을 맵에서 없앤다.(lock 종료)</td>
		<td>S2C_SetItemNoti<br>
		S2C_GetItemNoti</td>
	</tr>
	<tr>
		<td align="center">플레이어가 다른 플레이어에 닿는다</td>
		<td>1. A 플레이어가 이동한 위치가 다른 플레이어 범위와 겹치는지 확인한다.<br>
  2. 겹칠 경우 어느 플레이어가 점수가 더 높은지 확인한다.<br>
  3. 점수가 낮은 플레이어의 몸이 다른 플레이어의 몸과 30% 이상 겹쳐졌는지 확인한다.<br>
  4. 30% 미만으로 겹쳐져있을 경우, 패스한다.<br>
  5. 30% 이상으로 겹쳐져있을 경우, 점수가 낮은 플레이어를 데스처리하고, 해당 플레이어의 점수만큼을 다른 플레이어에게 지급한다.</td>
		<td>S2C_KillPlayerNoti<br>
		S2C_ScoreUpdateNoti</td>
	</tr>
	<tr>
		<td align="center">죽는다</td>
		<td>1. 플레이어가 죽을 경우, 점수가 1으로 초기화된다.<br>
  2. 5초 뒤 게임 맵에 캐릭터가 재생성된다.</td>
		<td>S2C_RespawnPlayerNoti</td>
	</tr>
	<tr>
		<td align="center">게임이 종료된다</td>
		<td>1. 게임시간이 끝나거나 특정 플레이어가 목표 점수에 도달한다.<br>
  2. 게임 랭킹(1~5등)을 띄운다.<br>
  3. 데이터베이스에 해당 게임 정보를 저장한다.(유저별 킬, 데스, 최대 점수)<br>
  4. 데이터베이스에 해당 게임 uid를 각 유저의 전적에 추가한다.</td>
	</tr>
	<tr>
		<td align="center">전적을 확인한다</td>
		<td>a. 게임을 처음 실행했을때 해당 유저의 게임 기록을 전달한다.<br>
  b. 게임이 종료되었을 때 해당 게임 기록을 전달한다.</td>
		<td>C2S_TotalHistoryReq<br>
		S2C_TotalHistoryRes<br>
		S2C_HistoryNoti</td>
	</tr>
</table>

## 서버 패킷 종류

### 클라이언트 → 서버

<table align="center">
	<tr>
		<td align="center"><b>패킷 이름</td>
		<td align="center"><b>패킷에 포함된 정보</td>
		<td align="center"><b>패킷 기능</td>
		<td align="center"><b>사용처</td>
	</tr>
	<tr>
		<td>C2S_LoginReq</td>
		<td>string ID<br>
		string Password</td>
		<td>클라이언트가 로그인 정보 입력 후 해당 로그인 정보를 토대로 서버에 로그인 요청</td>
		<td>로그인</td>
	</tr>
	<tr>
		<td>C2S_ResisterReq</td>
		<td>string ID<br>
		string Password</td>
		<td>클라이언트가 회원가입 정보를 입력 후 서버에게 회원가입 요청</td>
		<td>회원가입</td>
	</tr>
	<tr>
		<td>C2S_MatchMakingReq</td>
		<td>-</td>
		<td>서버에게 게임 매칭 시작을 요청</td>
		<td>매치메이킹</td>
	</tr>
	<tr>
		<td>C2S_DropInReq</td>
		<td>string PlayerName</td>
		<td>서버에게 게임 난입을 요청</td>
		<td>게임 난입</td>
	</tr>
	<tr>
		<td>C2S_MoveReq</td>
		<td>Direction Dir(struct)</td>
		<td>서버에게 조작을 요청</td>
		<td>조작</td>
	</tr>
	<tr>
		<td>C2S_TotalHistoryReq</td>
		<td>-</td>
		<td>서버에게 모든 전적을 요청</td>
		<td>전적</td>
	</tr>
</table>

### 서버 → 클라이언트

<table align="center">
	<tr>
		<td align="center"><b>패킷 이름</td>
		<td align="center"><b>패킷에 포함된 정보</td>
		<td align="center"><b>패킷 기능</td>
		<td align="center"><b>사용처</td>
	</tr>
	<tr>
		<td>S2C_LoginRes</td>
		<td>bool Result<br>
		LoginResponse Type(Enum)</td>
		<td>서버가 클라이언트에게 받은 로그인 정보를 토대로 로그인이 되었는지 결과를 전송</td>
		<td>로그인</td>
	</tr>
	<tr>
		<td>S2C_ResisterRes</td>
		<td>bool Result<br>
		ResisterResponse Type(Enum)</td>
		<td>클라이언트에게 받은 정보를 토대로 회원가입 결과를 전송</td>
		<td>회원가입</td>
	</tr>
	<tr>
		<td>S2C_MatchMakingRes</td>
		<td>bool Result<br>
		MatchMakingResponse Type(Enum)</td>
		<td>클라이언트에게 매칭 요청에 대한 결과를 전송</td>
		<td>매치메이킹</td>
	</tr>
	<tr>
		<td>S2C_MatchNoti</td>
		<td>-</td>
		<td>클라이언트에게 매칭 결과를 통보</td>
		<td>매치메이킹</td>
	</tr>
	<tr>
		<td>S2C_DropInRes</td>
		<td>bool Result<br>
		DropInResponse Type(Enum)</td>
		<td>클라이언트에게 난입 결과를 전송</td>
		<td>게임 난입</td>
	</tr>
	<tr>
		<td>S2C_GameStartNoti</td>
		<td>Date UtcTime</td>
		<td>클라이언트들에게 게임 시작을 통지</td>
		<td>게임 시작</td>
	</tr>
	<tr>
		<td>S2C_GameEndNoti</td>
		<td>string Winner<br>
		int Score</td>
		<td>클라이언트들에게 게임 종료를 통지</td>
		<td>게임 종료</td>
	</tr>
	<tr>
		<td>S2C_MoveRes</td>
		<td>bool Result</td>
		<td>클라이언트에게 조작 결과를 전송</td>
		<td>조작</td>
	</tr>
	<tr>
		<td>S2C_MoveNoti</td>
		<td>string PlayerName<br>
		Direction Dir(struct)</td>
		<td>클라이언트들에게 특정 캐릭터의 조작을 통지</td>
		<td>조작</td>
	</tr>
	<tr>
		<td>S2C_SetItemNoti</td>
		<td>Location loc(struct)</td>
		<td>클라이언트들에게 특정 위치에 아이템이 생성됨을 통지</td>
		<td>아이템</td>
	</tr>
	<tr>
		<td>S2C_GetItemNoti</td>
		<td>Location loc(struct)</td>
		<td>클라이언트들에게 특정 유저가 특정 위치의 아이템을 먹었음을 통지</td>
		<td>아이템</td>
	</tr>
	<tr>
		<td>S2C_KillPlayerNoti</td>
		<td>string Murderer<br>
		string DeadPlayer</td>
		<td>클라이언트들에게 특정 유저가 다른 유저를 죽였다는 것을 통지</td>
		<td>PK</td>
	</tr>
	<tr>
		<td>S2C_RespawnPlayerNoti</td>
		<td>string RespawnPlayer<br>
		Location Loc(struct)</td>
		<td>클라이언트들에게 특정 유저가 리스폰 했다는 것을 통지</td>
		<td>리스폰</td>
	</tr>
	<tr>
		<td>S2C_ScoreUpdateNoti</td>
		<td>string TargetPlayer<br>
		int Score</td>
		<td>클라이언트들에게 특정 유저의 점수에 변동이 있음을 통지</td>
		<td>점수</td>
	</tr>
	<tr>
		<td>S2C_TotalHistoryRes</td>
		<td>List<GameHistory> Histories(struct)</td>
		<td>클라이언트에게 모든 전적 전송에 대한 결과를 전송</td>
		<td>전적</td>
	</tr>
	<tr>
		<td>S2C_HistoryNoti</td>
		<td>GameHistory History(struct)</td>
		<td>클라이언트에게 해당 게임에 대한 전적을 통지</td>
		<td>전적</td>
	</tr>
</table>

## 작업 우선 순위

### 1순위

- 서버 접속(→로그인 서버/TCP 연결)
- 방 생성 로직
- 게임 시작(방 생성 로직 필요)
- 매칭 시스템(방 생성 로직 필요)
- 게임 타이머(게임 시작 필요)
- 게임 종료(게임 시작, 게임 타이머, 게임 점수 필요)
- 캐릭터 이동(게임 시작 필요)

### 2순위

- 회원가입/로그인(데이터베이스 DCL)
- 난입 시스템(매칭 시스템 필요)
- 게임 점수(게임 시작 필요)
- 아이템 생성(게임 시작 필요)
- 아이템 획득(게임 시작, 아이템 생성 필요)
- 캐릭터 사망 판정(게임 시작, 게임 점수 필요)

### 3순위

- 전적 저장(데이터베이스 DCL)