# Huong dan hoc luong chuc nang trong game

Tai lieu nay di theo dung code hien co cua project. Cach hoc tot nhat la mo file script o cot "File nen doc", sau do doc phan "Luong chay" va chay Play Mode de dat breakpoint hoac `Debug.Log` tai cac ham duoc nhac ten.

## 0. Ban do tong quan

Game chia thanh 5 cum lon:

| Cum | Vai tro | File nen doc |
| --- | --- | --- |
| Dieu phoi tran dau | Quan ly Loading, StartMatch, Pause, Victory, Lose | `Assets/_Game/Scripts/GamePlay/GameManager.cs` |
| Nhan vat | State machine Idle, Run, Attack, Slide, Hurt, Dead | `Character.cs`, `Player.cs`, `Enemy.cs` |
| Combat | Bat/tat vung gay sat thuong, tru mau, chet, drop item | `DamageCaster.cs`, `Health.cs`, `DamageOrb.cs` |
| Man choi | Spawn enemy, mo cong, xac dinh thang/thua | `Spawner.cs`, `SpawnPoint.cs`, `Gate.cs` |
| UI va online | Panel UI, Supabase room, Photon Fusion avatar | `UIManager.cs`, `PanelGamePlay.cs`, `RoomService.cs`, `FusionMatchBootstrap.cs`, `FusionPlayerAvatar.cs` |

Enum nen nam truoc:

```csharp
public enum GameState { Loading, Home, StartMatch, Pause, EndMatch, Victory, Lose }
public enum CharacterState { Idle, Run, Attack, Slide, Hurt, Dead }
public enum PickUpType { Health, Coin }
```

## 1. Luong vao game va trang thai tran dau

### Y tuong

`GameManager` la trung tam cua tran dau offline. No giu `CurrentState`, bat/tat `Time.timeScale`, mo UI gameplay, hien UI thang/thua va nghe su kien tu player/spawner.

### Luong chay

1. `Awake()` tao singleton `GameManager.Instance`.
2. Neu `autoFindSceneObjects = true`, `CacheSceneObjects()` tim `Player` va tat ca `Spawner`.
3. `Start()` goi `SubscribeSceneEvents()` de nghe:
   - `player.Died += OnPlayerDied`
   - `spawner.Cleared += OnSpawnerCleared`
4. `ChangeState(initialState)` vao trang thai dau tien, thuong la `Loading`.
5. `LoadingRoutine()` cho `loadingDuration`, sau do chuyen sang `stateAfterLoading`, thuong la `StartMatch`.

### Code minh hoa

```csharp
private void Start()
{
    SubscribeSceneEvents();
    ChangeState(initialState);
}

private IEnumerator LoadingRoutine()
{
    yield return new WaitForSecondsRealtime(loadingDuration);
    ChangeState(stateAfterLoading);
}
```

Khi vao `StartMatch`, game mo UI gameplay va cho thoi gian chay:

```csharp
case GameState.StartMatch:
    Time.timeScale = 1f;
    OpenGameplayUI();
    break;
```

Khi player chet offline, `GameManager` khong thua ngay, ma doi mot khoang:

```csharp
private void OnPlayerDied(Character deadCharacter)
{
    if (NetworkMatchManager.IsOnlineMatchActive()) return;
    delayedLoseCoroutine = StartCoroutine(DelayLoseAfterPlayerDeath());
}
```

Khi tat ca spawner da clear, game thang:

```csharp
private void OnSpawnerCleared(Spawner clearedSpawner)
{
    foreach (Spawner spawner in spawners)
        if (spawner != null && !spawner.IsCleared) return;

    Victory();
}
```

## 2. Luong state machine cua nhan vat

### Y tuong

`Character` la lop cha cho `Player` va `Enemy`. Moi nhan vat deu chay qua cac state: `Idle`, `Run`, `Attack`, `Slide`, `Hurt`, `Dead`.

`Character` khong tu biet input den tu dau. No bat lop con override:

```csharp
protected abstract Vector3 GetMoveDirection();
```

`Player` lay input tu ban phim/joystick. `Enemy` lay huong di tu NavMeshAgent.

### Luong Update

1. `Update()` hoac `FixedUpdate()` goi `MoveCharacter(deltaTime)`.
2. Neu dang `Dead` thi dung het movement.
3. Neu dang spawn dissolve thi khoa movement.
4. Goi `UpdateState(CurrentState, deltaTime)` de state hien tai tu xu ly.
5. Neu state cho phep di chuyen, lay huong tu `GetMoveDirection()`.
6. Lam muot huong di bang `Vector3.MoveTowards`.
7. Cap nhat Animator `Speed`, `IsGrounded`.
8. Di chuyen bang `CharacterController`, `Rigidbody`, hoac `transform.position`.
9. Neu co va cham/bi danh, ap dung impact knockback.

### Code minh hoa

```csharp
private void MoveCharacter(float deltaTime)
{
    if (CurrentState == CharacterState.Dead)
    {
        SetAnimatorFloat("Speed", 0f, 0f, deltaTime);
        return;
    }

    UpdateState(CurrentState, deltaTime);

    bool canMove = CanMoveInCurrentState();
    Vector3 targetMoveDirection = canMove ? GetMoveDirection() : Vector3.zero;
    smoothedMoveDirection = Vector3.MoveTowards(
        smoothedMoveDirection,
        targetMoveDirection,
        acceleration * deltaTime
    );

    MoveWithCharacterController(smoothedMoveDirection, speed, canMove, hasMoveInput, deltaTime);
}
```

Ham quan trong nhat de chuyen state:

```csharp
public void SwitchToState(CharacterState newState, bool forceRestart = false)
{
    if (CurrentState == newState && !forceRestart) return;

    ExitState(CurrentState);
    CurrentState = newState;
    EnterState(CurrentState);
}
```

Can nho: moi state co 3 diem mo rong:

```csharp
protected virtual void OnEnterAttack() { }
protected virtual void OnUpdateAttack(float deltaTime) { }
protected virtual void OnExitAttack() { }
```

`Player` va `Enemy` override cac ham nay de tao hanh vi rieng.

## 3. Luong di chuyen cua Player

### Y tuong

`Player.GetMoveDirection()` uu tien joystick neu co joystick va khong co keyboard. Neu co keyboard, input duoc lam muot de nhan vat khong giat.

### Luong chay

1. Doc keyboard bang `ReadKeyboardMoveInput()`.
2. Doc joystick bang `JoyStickController.direct`.
3. Neu joystick co input va keyboard khong co input, dung joystick.
4. Neu keyboard co input, lam muot bang `keyboardInputAcceleration`.
5. Khi nha phim, giam ve 0 bang `keyboardInputDeceleration`.

### Code minh hoa

```csharp
protected override Vector3 GetMoveDirection()
{
    Vector3 keyboardDirection = ReadKeyboardMoveInput();
    Vector3 joystickDirection = JoyStickController.direct;

    bool hasKeyboardInput = keyboardDirection.sqrMagnitude > 0.001f;
    bool hasJoystickInput = joystickDirection.sqrMagnitude > 0.001f;

    if (hasJoystickInput && !hasKeyboardInput)
        return Vector3.ClampMagnitude(joystickDirection, 1f);

    smoothedKeyboardMoveInput = Vector3.MoveTowards(
        smoothedKeyboardMoveInput,
        hasKeyboardInput ? keyboardDirection : Vector3.zero,
        inputSmoothingSpeed * Time.deltaTime
    );

    return Vector3.ClampMagnitude(smoothedKeyboardMoveInput, 1f);
}
```

Nut UI trong `PanelGamePlay` cung goi vao player:

```csharp
public void OnSlideButtonClicked()
{
    if (fusionPlayerAvatar != null)
        fusionPlayerAvatar.RequestSlide();
    else
        player.Slide();
}
```

Nghia la:

- Offline: UI -> `Player.Slide()`.
- Online: UI -> `FusionPlayerAvatar.RequestSlide()`.

## 4. Luong tan cong combo cua Player

### Y tuong

Player co combo toi da 3 don. Bam attack khi dang attack se queue combo tiep theo. Neu nguoi choi di chuyen trong luc attack, combo dang queue bi huy.

### Luong chay

1. UI hoac phim Enter goi `Player.Attack()`.
2. Neu dang `Slide`, `Hurt`, `Dead` thi bo qua.
3. Neu dang `Attack`, goi `QueueNextCombo()`.
4. Neu chua attack, set `requestedComboCount = 1`, vao state `Attack`.
5. `OnEnterAttack()` goi `BeginAttackCombo(1)`.
6. `BeginAttack()` set timer, trigger Animator `"Attack"`.
7. Khi animation ket thuc, animation event goi `AttackAnimationEnds()`.
8. `CompleteCurrentCombo()` quyet dinh danh don tiep hay thoat ve `Idle/Run`.

### Code minh hoa

```csharp
public void Attack()
{
    if (CurrentState == CharacterState.Slide
        || CurrentState == CharacterState.Hurt
        || CurrentState == CharacterState.Dead)
        return;

    if (CurrentState == CharacterState.Attack)
    {
        QueueNextCombo();
        return;
    }

    requestedComboCount = 1;
    SwitchToState(CharacterState.Attack);
}
```

Doan quyet dinh combo:

```csharp
private void CompleteCurrentCombo()
{
    if (!cancelQueuedCombosForMove
        && currentComboIndex < requestedComboCount
        && currentComboIndex < MaxComboCount)
    {
        BeginAttackCombo(currentComboIndex + 1);
        return;
    }

    FinishAttack();
}
```

## 5. Luong gay sat thuong can chien

### Y tuong

Vu khi/ vung chem dung `DamageCaster`. Collider cua `DamageCaster` mac dinh tat. Khi animation attack den frame chem, animation event se goi `EnableDamageCaster()`. Khi het frame gay damage, goi `DisableDamageCaster()`.

### Luong offline

1. `DamageCaster.OnTriggerEnter/Stay()` goi `TryApplyDamage(other)`.
2. Neu da danh target trong lan chem nay, bo qua bang `damageTargetIdSet`.
3. Tim `Character` tren collider bi cham.
4. Kiem tra tag target.
5. Goi `targetCharacter.ApplyDamage(damage, attackerPosition)`.
6. Player VFX phat slash hit.

### Code minh hoa

```csharp
private void TryApplyDamage(Collider other)
{
    Character targetCharacter = other.GetComponentInParent<Character>();

    if (targetCharacter == null || !targetCharacter.CompareTag(targetTag))
        return;

    if (HasDamagedTarget(targetCharacter))
        return;

    targetCharacter.ApplyDamage(damage, attackerPosition);
    PlayHitVFX(other);
    MarkDamagedTarget(targetCharacter);
}
```

### Luong khi bi danh

`Character.ApplyDamage()` la noi tru mau, bat blink, hurt, knockback, hoac chet.

```csharp
public void ApplyDamage(int damage, Vector3 attackPos = new Vector3())
{
    if (Health == null || Health.IsDead) return;

    Health.ApplyDamage(damage);

    if (Health.IsDead)
    {
        SwitchToState(CharacterState.Dead);
        return;
    }

    PlayMaterialsBlink();
    SwitchToState(CharacterState.Hurt, true);
    AddImpact(attackPos, HurtImpactForce);
}
```

## 6. Luong slide / ne don

### Y tuong

Slide la state rieng cua `Player`. Khi slide:

- Khong cho attack tiep.
- Tat `DamageCaster`.
- Di chuyen theo `slideDirection`.
- Het timer/animation thi ve `Run` neu con input, nguoc lai ve `Idle`.

### Code minh hoa

```csharp
public void Slide()
{
    if (CurrentState == CharacterState.Attack
        || CurrentState == CharacterState.Slide
        || CurrentState == CharacterState.Hurt
        || CurrentState == CharacterState.Dead)
        return;

    slideDirection = GetSlideDirection();
    SwitchToState(CharacterState.Slide, true);
}

protected override void OnUpdateSlide(float deltaTime)
{
    Vector3 movement = slideDirection * (slideDistance / slideDuration) * deltaTime;
    MoveBy(movement);
    RotateTowards(slideDirection, deltaTime);
}
```

## 7. Luong AI Enemy

### Y tuong

`Enemy` ke thua `Character`, nhung input khong den tu nguoi choi. `Enemy.GetMoveDirection()` hoi `NavMeshAgent` xem nen di ve dau.

### Luong chay

1. `Awake()` lay `NavMeshAgent`, tat `agent.updatePosition/updateRotation` de code tu dieu khien transform.
2. `RefreshClosestPlayerTarget()` tim player gan nhat con song va co duong NavMesh hop le.
3. `GetMoveDirection()`:
   - Neu trong tam danh: dung lai.
   - Neu den luc repath: `agent.SetDestination(target.position)`.
   - Lay huong tu `agent.steeringTarget - transform.position`.
   - Ap dung tach nhau bang `ApplyEnemySeparation()`.
4. `OnUpdateIdle/Run()` kiem tra neu du tam danh thi vao state `Attack`.
5. `OnEnterAttack()` dung agent, quay mat ve player, trigger Animator `"Attack"`.

### Code minh hoa

```csharp
protected override Vector3 GetMoveDirection()
{
    if (IsTargetInAttackRange())
    {
        StopAgentPath();
        return Vector3.zero;
    }

    if (Time.time >= nextRepathTime)
    {
        nextRepathTime = Time.time + repathInterval;
        agent.SetDestination(target.position);
    }

    Vector3 direction = agent.steeringTarget - transform.position;
    return ApplyEnemySeparation(direction.normalized);
}
```

Doan vao attack:

```csharp
private void TryEnterAttackState()
{
    if (IsTargetInAttackRange())
        SwitchToState(CharacterState.Attack);
}
```

## 8. Luong spawn enemy, clear wave, mo cong

### Y tuong

`Spawner` la trigger vung. Khi player di vao, spawner sinh enemy tu cac `SpawnPoint`. Moi enemy sinh ra duoc dang ky su kien `Died`. Khi tat ca enemy chet, spawner clear va mo cong.

### Luong offline

1. Player cham collider cua `Spawner`.
2. `OnTriggerEnter()` goi `SpawnCharacters()`.
3. Neu khong co `NetworkRunner`, goi `SpawnOfflineCharacters()`.
4. Lap qua moi `SpawnPoint`.
5. Instantiate enemy prefab.
6. `aliveEnemyCount++`.
7. `spawnedCharacter.Died += OnSpawnedCharacterDied`.
8. Enemy chet thi `aliveEnemyCount--`.
9. Neu count ve 0, `ClearSpawner()`.
10. `OpenGate()` goi `gate.OpenGate()`.
11. `Cleared?.Invoke(this)` bao `GameManager`.

### Code minh hoa

```csharp
private void OnTriggerEnter(Collider other)
{
    if (other.CompareTag("Player"))
        SpawnCharacters();
}

private void OnSpawnedCharacterDied(Character spawnedCharacter)
{
    aliveEnemyCount = Mathf.Max(aliveEnemyCount - 1, 0);

    if (aliveEnemyCount <= 0)
        ClearSpawner();
}

private void ClearSpawner()
{
    hasCleared = true;
    OpenGate();
    Cleared?.Invoke(this);
}
```

`Gate.OpenGate()` keo visual xuong trong `OpenDuration`, sau do tat collider:

```csharp
private IEnumerator OpenGateAnimation()
{
    gateVisual.transform.position = Vector3.Lerp(startPos, targetPos, t);
    gateCollider.enabled = false;
}
```

## 9. Luong mau, chet, dissolve va drop item

### Health

`Health` chi lam viec rat gon:

- `currentHealth`
- `maxHealth`
- `ApplyDamage()`
- `AddHealth()`
- event `HealthChanged`

```csharp
public void ApplyDamage(int damage)
{
    if (IsDead || damage <= 0) return;

    currentHealth = Mathf.Max(currentHealth - damage, 0);
    HealthChanged?.Invoke(currentHealth, maxHealth);
}
```

### Death trong Character

Khi mau ve 0:

1. `Character.ApplyDamage()` goi `SwitchToState(Dead)`.
2. `OnEnterDead()`:
   - Luu vi tri drop.
   - Goi event `Died`.
   - Trigger Animator `"Dead"`.
   - Tat `DamageCaster`.
   - Bat dissolve.
3. Sau dissolve, `DropItem()` instantiate item hoac spawn network item.

```csharp
protected virtual void OnEnterDead()
{
    deathDropPosition = transform.position;
    NotifyDied();
    SetAnimatorTrigger("Dead");
    DisableDamageCaster();
    StartMaterialDissolve();
}
```

## 10. Luong nhat item, hoi mau, cong coin

### Offline

1. Player cham pickup.
2. `PickUp.OnTriggerEnter()` kiem tra tag `"Player"`.
3. Lay `Character`.
4. Goi `character.ApplyPickupValue(type, value)`.
5. Phat VFX.
6. Destroy pickup.

```csharp
private void OnTriggerEnter(Collider other)
{
    Character character = other.GetComponentInParent<Character>();
    collected = true;
    character.ApplyPickupValue(type, value);
    PlayCollectedVFX(transform.position);
    Destroy(gameObject);
}
```

Trong `Character`:

```csharp
public void ApplyPickupValue(PickUpType pickupType, int value)
{
    switch (pickupType)
    {
        case PickUpType.Health: AddHealth(value); break;
        case PickUpType.Coin: AddCoin(value); break;
    }
}
```

Khi coin doi, `Character` phat event:

```csharp
private void AddCoin(int coin)
{
    Coin += coin;
    CoinChanged?.Invoke(Coin);
}
```

`PanelGamePlay` nghe event nay de cap nhat text va luu Supabase:

```csharp
private void OnPlayerCoinChanged(int coin)
{
    coinText.text = coin.ToString();
    SupabaseSession.AddCoin(collectedCoin);
    QueueCoinSave(SupabaseSession.Coin);
}
```

## 11. Luong UI gameplay

### UIManager

`UIManager` load tat ca prefab trong `Resources/UI/`, luu theo type, va instantiate khi can.

```csharp
private void Awake()
{
    UICanvas[] prefabs = Resources.LoadAll<UICanvas>("UI/");
    foreach (UICanvas prefab in prefabs)
        canvasPrefabs.Add(prefab.GetType(), prefab);
}

public T OpenUI<T>() where T : UICanvas
{
    T canvas = GetUI<T>();
    canvas.SetUp();
    canvas.Open();
    return canvas;
}
```

### PanelGamePlay

Panel nay lam 5 viec:

1. Tim player local.
2. Bind nut attack/slide.
3. Cap nhat health slider.
4. Cap nhat coin text va save coin.
5. Online: hien revive button khi co dong doi bi down gan do.

Nut attack:

```csharp
public void OnAttackButtonClicked()
{
    if (fusionPlayerAvatar != null)
        fusionPlayerAvatar.RequestAttack();
    else
        player.Attack();
}
```

Health bar:

```csharp
private void OnPlayerHealthChanged(int currentHealth, int maxHealth)
{
    healthSlider.maxValue = Mathf.Max(1, maxHealth);
    healthSlider.SetValueWithoutNotify(currentHealth);
}
```

Revive:

```csharp
private void UpdateReviveButton(float deltaTime)
{
    FusionPlayerAvatar reviveTarget = FindNearestReviveTarget();
    if (reviveTarget == null) return;

    reviveHoldTimer += deltaTime;
    if (reviveHoldTimer >= reviveHoldDuration)
        fusionPlayerAvatar.RequestReviveTarget(currentReviveTarget);
}
```

## 12. Luong online: dang nhap, tao phong, vao tran

### Dang nhap Supabase

`AuthService.SignIn()`:

1. Goi Supabase Auth endpoint `/auth/v1/token?grant_type=password`.
2. Parse `access_token`, `refresh_token`, `user`.
3. Luu vao `SupabaseSession`.
4. Goi `LoadUserProfile()` de lay username/avatar/coin.
5. Cleanup room/match cu.

```csharp
SupabaseSession.AccessToken = response.access_token;
SupabaseSession.UserId = response.user.id;
SupabaseSession.Email = response.user.email;
SupabaseSession.DisplayName = response.user.GetDisplayName();
yield return LoadUserProfile(SupabaseSession.UserId, ...);
```

### Tao/join room

`RoomService` goi Supabase Edge Functions:

```csharp
CreateRoom() -> create_room
JoinRoom()   -> join_room
SetReady()   -> set_ready
StartMatch() -> start_match
```

`PanelRoomMatch` la UI cua phong:

1. `SetRoom()` luu room vao `OnlineRoomSession`.
2. `RefreshRoutine()` lap moi 2 giay:
   - Gui heartbeat.
   - Load danh sach player.
   - Kiem tra active match.
3. Host bam Start Match -> `StartMatchAsHostRoutine()`.
4. Client khong phai host bam Ready -> `roomService.SetReady(...)`.
5. Khi co match, `BeginLoadMatch()` load `GameScene`.

```csharp
private void BeginLoadMatch(RoomService.MatchData match)
{
    OnlineRoomSession.SetMatch(match);
    OnlineRoomSession.CacheExpectedMatchPlayerCount();
    OnlineMatchLoadingOverlay.LoadScene(gameSceneName);
}
```

## 13. Luong Fusion khi vao GameScene online

### FusionMatchBootstrap

`FusionMatchBootstrap.Start()` la entry point cua online match trong scene game.

Luong chay:

1. Lay `OnlineRoomSession.MatchId` lam `SessionName`.
2. Neu khong co match id va cho fallback, giu player offline.
3. Goi `OnlineMatchStats.StartMatch(...)`.
4. Tat scene player template.
5. Tao `NetworkRunner`.
6. `runner.StartGame(GameMode.Shared, SessionName = matchId)`.
7. Khi join thanh cong, spawn player network local.

```csharp
StartGameResult result = await runner.StartGame(new StartGameArgs
{
    GameMode = GameMode.Shared,
    SessionName = sessionName,
    PlayerCount = maxPlayers,
    AuthValues = new AuthenticationValues(GetPhotonUserId())
});

SpawnLocalPlayerIfNeeded(runner.LocalPlayer);
```

Spawn player:

```csharp
localPlayerObject = runner.Spawn(networkPlayerPrefab, spawnPosition, spawnRotation, player);
runner.SetPlayerObject(player, localPlayerObject);
```

## 14. Luong FusionPlayerAvatar

### Vai tro

`FusionPlayerAvatar` thay the `Player` offline khi online. No tu dieu khien movement, attack, slide, damage, revive, nameplate va dong bo health.

Khi object network spawn:

```csharp
public override void Spawned()
{
    ResolveReferences();
    SubscribeHealth();
    InitializeReviveState();
    ApplyAuthorityState();
    SetLocalIdentityIfNeeded();
    RefreshDisplayNameView();
}
```

`ApplyAuthorityState()` phan biet local/remote:

```csharp
bool isLocalPlayer = HasLocalControl();
gameObject.tag = isLocalPlayer ? "Player" : "Untagged";
player.enabled = false; // movement online do FusionPlayerAvatar dieu khien
```

### Input online

UI goi:

```csharp
fusionPlayerAvatar.RequestAttack();
fusionPlayerAvatar.RequestSlide();
```

Trong avatar:

```csharp
public void RequestAttack()
{
    if (!HasLocalControl()) return;
    attackQueued = true;
}
```

`FixedUpdateNetwork()` moi tick se:

1. Kiem tra local control.
2. Xu ly queue attack/slide.
3. Cap nhat damage window.
4. Di chuyen bang `CharacterController`.
5. Ghi `NetworkedSpeed`, `NetworkedGrounded`.

```csharp
public override void FixedUpdateNetwork()
{
    if (!HasLocalControl()) return;

    ConsumeQueuedActions();
    UpdateAttackDamageWindow(Runner.DeltaTime);
    SimulateMovement(Runner.DeltaTime);
}
```

### Damage online

Khi `DamageCaster` cham enemy/player network, no khong tru mau truc tiep tren moi may. No goi request toi object co authority:

```csharp
targetNetworkPlayer.RequestDamage(damage, attackerPosition, damageSourceId);
```

Trong avatar:

```csharp
public bool RequestDamage(int damage, Vector3 attackPosition, int damageSourceId = 0)
{
    RPC_ApplyDamage(damage, attackPosition, damageSourceId);
    return true;
}

[Rpc(RpcSources.All, RpcTargets.StateAuthority)]
private void RPC_ApplyDamage(int damage, Vector3 attackPosition, int damageSourceId)
{
    player.ApplyDamage(damage, attackPosition);
    networkHealth?.ForceSyncNow();
}
```

`FusionNetworkHealth` dong bo mau:

```csharp
public override void FixedUpdateNetwork()
{
    if (Object.HasStateAuthority)
        MirrorLocalHealthToNetwork();
}

public override void Render()
{
    ApplyNetworkHealthToLocal();
}
```

### Chet, down, revive

Khi health ve 0:

1. `HealthChanged` goi `FusionPlayerAvatar.OnHealthChanged`.
2. `ApplyNetworkDeath()` dung control local.
3. Neu con revive, set `IsDowned = true`.
4. Neu het revive, set `IsEliminated = true`.
5. Chuyen `Character` sang `Dead`.

```csharp
private void OnHealthChanged(int current, int max)
{
    if (current > 0 || hasAppliedNetworkDeath) return;
    ApplyNetworkDeath();
}
```

Revive:

1. Player song dung gan player down.
2. `PanelGamePlay.UpdateReviveButton()` giu nut revive.
3. Goi `fusionPlayerAvatar.RequestReviveTarget(target)`.
4. Target nhan `RPC_RequestRevive(reviver)`.
5. Authority kiem tra khoang cach, trang thai, revive count.
6. Set lai health va broadcast `RPC_ApplyRevive`.

```csharp
[Rpc(RpcSources.All, RpcTargets.StateAuthority)]
private void RPC_RequestRevive(PlayerRef reviver)
{
    if (!CanBeRevived) return;

    RevivesRemaining--;
    IsDowned = false;
    IsEliminated = false;
    health.SetHealthFromNetwork(reviveHealth, maxHealth);
    RPC_ApplyRevive(reviveHealth);
}
```

## 15. Luong thang/thua online

`NetworkMatchManager` chay o moi client va danh gia tran dau online.

Moi `Update()`:

1. Neu online match dang active.
2. Doi qua `evaluationStartDelay`.
3. Neu tat ca spawner cleared -> Victory.
4. Neu het thoi gian -> Lose.
5. Neu tat ca player khong the tiep tuc -> Lose.
6. Broadcast result qua `FusionPlayerAvatar`.
7. Moi client nhan RPC va goi `GameManager.Victory()` hoac `GameManager.Lose()`.

```csharp
private void EvaluateMatchState()
{
    if (AreAllSpawnersCleared())
    {
        FinishMatch(GameState.Victory);
        return;
    }

    if (HasMatchTimeExpired() || AreAllPlayersUnableToContinue())
        FinishMatch(GameState.Lose);
}
```

Broadcast:

```csharp
private bool BroadcastResult(GameState resultState)
{
    return avatar.BroadcastMatchResult(resultState);
}

[Rpc(RpcSources.All, RpcTargets.All)]
private void RPC_ApplyMatchResult(int resultStateValue)
{
    NetworkMatchManager.Ensure().ApplyNetworkResult((GameState)resultStateValue);
}
```

## 16. Cach tu hoc va debug theo luong

Nen hoc theo thu tu nay:

1. `GameManager.ChangeState()` de hieu tran dau bat dau/ket thuc.
2. `Character.MoveCharacter()` de hieu vong lap nhan vat.
3. `Player.Attack()` va `Player.Slide()` de hieu input offline.
4. `DamageCaster.TryApplyDamage()` va `Character.ApplyDamage()` de hieu combat.
5. `Enemy.GetMoveDirection()` de hieu AI.
6. `Spawner.SpawnCharacters()` de hieu wave va gate.
7. `PanelGamePlay.OnAttackButtonClicked()` de hieu UI noi vao gameplay.
8. `RoomService.StartMatch()` va `FusionMatchBootstrap.Start()` de hieu online.
9. `FusionPlayerAvatar.FixedUpdateNetwork()` de hieu gameplay online.
10. `NetworkMatchManager.EvaluateMatchState()` de hieu thang/thua online.

Dat breakpoint goi y:

```csharp
GameManager.ChangeState()
Character.SwitchToState()
Player.Attack()
DamageCaster.TryApplyDamage()
Character.ApplyDamage()
Spawner.ClearSpawner()
PanelGamePlay.OnPlayerCoinChanged()
FusionPlayerAvatar.RPC_ApplyDamage()
NetworkMatchManager.FinishMatch()
```

Meo doc code: moi khi gap event hoac RPC, hay hoi "ai subscribe/goi ham nay?". Vi du:

- `Character.Died` duoc `GameManager` va `Spawner` nghe.
- `Health.HealthChanged` duoc `PanelGamePlay` va `FusionPlayerAvatar/FusionNetworkHealth` nghe.
- `Spawner.Cleared` duoc `GameManager` nghe offline, va duoc broadcast online qua `FusionPlayerAvatar`.
- `RPC_ApplyDamage` chi chay tren StateAuthority, sau do health duoc mirror ve cac client.

