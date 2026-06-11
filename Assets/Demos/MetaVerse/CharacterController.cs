using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public enum CharacterPlayer {
  Player1,
  Player2
}

public class CharacterController : MonoBehaviour
{
    public CharacterPlayer Player = CharacterPlayer.Player1;
    public float WalkSpeed = 3;
    public float StrafeSpeed = 3;
    public float TurnSpeed = 720;
    public bool UseCameraRelativeMovement = true;
    public float BonusSpeedIncrement = 0.5f;
    public float MaxBonusSpeedMultiplier = 2f;
    public float BonusSpeedDuration = 5f;
    public LayerMask CharacterLayers = 1 << 6;
    public float AttackRange = 1.6f;
    public float AttackCooldown = 0.8f;
    public float AttackSlowMultiplier = 0.35f;
    public float AttackSlowDuration = 1.25f;
    public float AttackKnockback = 1.25f;
    public int HitsBeforeKnockDown = 3;
    public float HitComboWindow = 2f;
    public float HitAnimationDuration = 0.45f;
    public float KnockDownDuration = 1.15f;
    public float StandUpDuration = 1.1f;
    public Key Player1AttackKey = Key.RightCtrl;
    public Key Player2AttackKey = Key.Space;
    public Key Player1InteractKey = Key.RightShift;
    public Key Player2InteractKey = Key.E;
    public float CarEnterRadius = 2.4f;

    Animator Anim;
    MetaverseInput inputs;
    InputAction PlayerAction;
    Rigidbody rb;
    Renderer[] renderers;
    Vector3 baseScale;
    float nextAttackTime;
    float slowedUntil;
    float speedBoostUntil;
    float bonusSpeedMultiplier = 1f;
    Coroutine hitFeedback;
    Coroutine hitAnimationRoutine;
    Coroutine attackFeedback;
    Coroutine knockDownRoutine;
    DrivableCar currentCar;
    Collider[] colliders;
    int comboHitsReceived;
    float lastHitTime;
    bool isDriving;
    bool isKnockedDown;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Anim = GetComponent<Animator>();
        inputs = new MetaverseInput();
        switch (Player) {
          case CharacterPlayer.Player1:
            PlayerAction = inputs.Player1.Move;
            break;
          case CharacterPlayer.Player2:
            PlayerAction = inputs.Player2.Move;
            break;
        }

        PlayerAction.Enable();

        rb = GetComponent<Rigidbody>();
        renderers = GetComponentsInChildren<Renderer>();
        colliders = GetComponentsInChildren<Collider>();
        baseScale = transform.localScale;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (isDriving || isKnockedDown) {
          Anim.SetFloat("Walk", 0f);
          return;
        }

        rb.angularVelocity = Vector3.zero;

        Vector2 vec = PlayerAction.ReadValue<Vector2>();
        Vector3 movement = GetMovementDirection(vec);
        float moveAmount = Mathf.Clamp01(movement.magnitude / Mathf.Max(WalkSpeed, StrafeSpeed));

        Anim.SetFloat("Walk", moveAmount);
        Anim.speed = IsSlowed ? 0.55f : 1f;

        if (movement.sqrMagnitude > 0.001f) {
          Quaternion targetRotation = Quaternion.LookRotation(movement.normalized, Vector3.up);
          Quaternion nextRotation = Quaternion.RotateTowards(rb.rotation, targetRotation, TurnSpeed * Time.fixedDeltaTime);
          rb.MoveRotation(nextRotation);
        }

        rb.MovePosition(rb.position + movement * CurrentSpeedMultiplier * Time.fixedDeltaTime);
    }

    Vector3 GetMovementDirection(Vector2 input)
    {
      if (!UseCameraRelativeMovement || Camera.main == null) {
        return new Vector3(input.x * StrafeSpeed, 0f, input.y * WalkSpeed);
      }

      Transform cameraTransform = Camera.main.transform;
      Vector3 cameraForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
      Vector3 cameraRight = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;

      if (cameraForward.sqrMagnitude < 0.001f || cameraRight.sqrMagnitude < 0.001f) {
        return new Vector3(input.x * StrafeSpeed, 0f, input.y * WalkSpeed);
      }

      return cameraRight * input.x * StrafeSpeed + cameraForward * input.y * WalkSpeed;
    }

    void Update()
    {
        if (Keyboard.current == null) { return; }

        Key interactKey = Player == CharacterPlayer.Player1 ? Player1InteractKey : Player2InteractKey;
        if (Keyboard.current[interactKey].wasPressedThisFrame) {
          ToggleCar();
        }

        if (isDriving || isKnockedDown) { return; }

        Key attackKey = Player == CharacterPlayer.Player1 ? Player1AttackKey : Player2AttackKey;
        if (Keyboard.current[attackKey].wasPressedThisFrame) {
          TryAttack();
        }
    }

    void OnDisable() {
      if (PlayerAction != null) {
        PlayerAction.Disable();
      }

      if (Anim != null) {
        Anim.speed = 1f;
      }
    }

    void OnDestroy() {
      inputs?.Dispose();
    }

    bool IsSlowed {
      get { return Time.time < slowedUntil; }
    }

    float CurrentSpeedMultiplier {
      get {
        float multiplier = IsSlowed ? AttackSlowMultiplier : 1f;
        if (IsSpeedBoosted) {
          multiplier *= bonusSpeedMultiplier;
        }

        return multiplier;
      }
    }

    bool IsSpeedBoosted {
      get { return Time.time < speedBoostUntil; }
    }

    public void ApplyBonusSpeedBoost()
    {
      if (!IsSpeedBoosted) {
        bonusSpeedMultiplier = 1f;
      }

      bonusSpeedMultiplier = Mathf.Min(MaxBonusSpeedMultiplier, bonusSpeedMultiplier + BonusSpeedIncrement);
      speedBoostUntil = Mathf.Max(speedBoostUntil, Time.time + BonusSpeedDuration);
    }

    public Vector2 GetMoveInput()
    {
      return PlayerAction != null ? PlayerAction.ReadValue<Vector2>() : Vector2.zero;
    }

    /// <summary>
    /// Direction de déplacement voulue, en espace monde, normalisée dans ~[-1,1]
    /// (déjà relative à la caméra). Utilisée par le client pour envoyer INPUT au serveur autoritaire.
    /// </summary>
    public Vector3 GetDesiredWorldMove()
    {
      Vector3 dir = GetMovementDirection(GetMoveInput());
      float maxSpeed = Mathf.Max(WalkSpeed, StrafeSpeed);
      if (maxSpeed > 0.001f) {
        dir /= maxSpeed;
      }
      return dir;
    }

    public bool IsDrivingCar {
      get { return isDriving; }
    }

    NetworkManager _net;
    NetworkManager Net {
      get {
        if (_net == null) _net = FindFirstObjectByType<NetworkManager>();
        return _net;
      }
    }

    void ToggleCar()
    {
      if (isDriving) {
        ExitCar();
        if (Net != null && Net.HasSession) {
          Net.SendCarExit();
        }
        return;
      }

      bool entered = TryEnterNearestCar();
      if (entered && Net != null && Net.HasSession) {
        string carId = currentCar != null && !string.IsNullOrEmpty(currentCar.ServerId)
          ? currentCar.ServerId
          : currentCar != null ? currentCar.gameObject.name : "";
        Net.SendCarEnter(carId);
      }
    }

    bool TryEnterNearestCar()
    {
      Collider[] hits = Physics.OverlapSphere(transform.position, CarEnterRadius, ~0, QueryTriggerInteraction.Collide);
      DrivableCar closest = null;
      float closestDistance = float.MaxValue;

      foreach (Collider hit in hits) {
        DrivableCar car = hit.GetComponentInParent<DrivableCar>();
        if (car == null) {
          car = TryMakeCarDrivable(hit.transform);
        }
        if (car == null || !car.CanEnter(this)) { continue; }

        float distance = Vector3.SqrMagnitude(car.transform.position - transform.position);
        if (distance < closestDistance) {
          closest = car;
          closestDistance = distance;
        }
      }

      if (closest != null) {
        closest.Enter(this);
        return true;
      }

      return false;
    }

    DrivableCar TryMakeCarDrivable(Transform hitTransform)
    {
      Transform root = null;
      Transform current = hitTransform;

      while (current != null) {
        if (IsCarObjectName(current.name)) {
          root = current;
        }

        current = current.parent;
      }

      if (root == null) { return null; }

      return root.gameObject.AddComponent<DrivableCar>();
    }

    bool IsCarObjectName(string objectName)
    {
      return objectName.Length > 3 && objectName.StartsWith("Car") && char.IsDigit(objectName[3]);
    }

    public void EnterCar(DrivableCar car)
    {
      currentCar = car;
      isDriving = true;
      rb.isKinematic = true;
      SetCharacterVisible(false);
      transform.SetParent(car.Seat, false);
      transform.localPosition = Vector3.zero;
      transform.localRotation = Quaternion.identity;
    }

    void ExitCar()
    {
      if (currentCar == null) { return; }

      DrivableCar car = currentCar;
      currentCar = null;
      isDriving = false;
      transform.SetParent(null, true);
      transform.position = car.GetExitPosition();
      transform.rotation = Quaternion.LookRotation(car.transform.right, Vector3.up);
      rb.isKinematic = false;
      rb.angularVelocity = Vector3.zero;
      SetCharacterVisible(true);
      car.Exit(this);
    }

    void SetCharacterVisible(bool visible)
    {
      foreach (Renderer currentRenderer in renderers) {
        currentRenderer.enabled = visible;
      }

      foreach (Collider currentCollider in colliders) {
        currentCollider.enabled = visible;
      }
    }

    void TryAttack()
    {
      if (isKnockedDown) { return; }
      if (Time.time < nextAttackTime) { return; }

      nextAttackTime = Time.time + AttackCooldown;

      CharacterController target = FindAttackTarget();
      if (target != null) {
        target.ReceiveHit(transform.position, AttackSlowDuration);
      }

      if (attackFeedback != null) {
        StopCoroutine(attackFeedback);
      }
      attackFeedback = StartCoroutine(PlayAttackFeedback(target));
    }

    CharacterController FindAttackTarget()
    {
      Collider[] hits = Physics.OverlapSphere(transform.position, AttackRange, CharacterLayers, QueryTriggerInteraction.Ignore);
      CharacterController closest = null;
      float closestDistance = float.MaxValue;

      foreach (Collider hit in hits) {
        CharacterController candidate = hit.GetComponentInParent<CharacterController>();
        if (candidate == null || candidate == this) { continue; }

        float distance = Vector3.SqrMagnitude(candidate.transform.position - transform.position);
        if (distance < closestDistance) {
          closest = candidate;
          closestDistance = distance;
        }
      }

      return closest;
    }

    void ReceiveHit(Vector3 attackerPosition, float duration)
    {
      if (isKnockedDown) { return; }

      slowedUntil = Mathf.Max(slowedUntil, Time.time + duration);

      Vector3 knockbackDirection = transform.position - attackerPosition;
      knockbackDirection.y = 0f;
      if (rb != null && knockbackDirection.sqrMagnitude > 0.001f) {
        rb.AddForce(knockbackDirection.normalized * AttackKnockback, ForceMode.VelocityChange);
      }

      if (hitFeedback != null) {
        StopCoroutine(hitFeedback);
      }
      hitFeedback = StartCoroutine(PlayHitFeedback());

      if (Time.time - lastHitTime > HitComboWindow) {
        comboHitsReceived = 0;
      }

      comboHitsReceived++;
      lastHitTime = Time.time;

      if (comboHitsReceived >= HitsBeforeKnockDown) {
        comboHitsReceived = 0;
        if (knockDownRoutine != null) {
          StopCoroutine(knockDownRoutine);
        }
        if (hitAnimationRoutine != null) {
          StopCoroutine(hitAnimationRoutine);
          hitAnimationRoutine = null;
        }
        knockDownRoutine = StartCoroutine(PlayKnockDownSequence());
      } else {
        string hitState = comboHitsReceived % 2 == 0 ? "Hit_B" : "Hit_A";
        if (hitAnimationRoutine != null) {
          StopCoroutine(hitAnimationRoutine);
        }
        hitAnimationRoutine = StartCoroutine(PlayHitAnimation(hitState));
      }
    }

    IEnumerator PlayAttackFeedback(CharacterController target)
    {
      Vector3 punchDirection = target != null ? target.transform.position - transform.position : transform.forward;
      punchDirection.y = 0f;
      if (punchDirection.sqrMagnitude < 0.001f) {
        punchDirection = transform.forward;
      }

      Quaternion startRotation = transform.rotation;
      Quaternion punchRotation = Quaternion.LookRotation(punchDirection.normalized, Vector3.up);
      Vector3 punchScale = new Vector3(baseScale.x * 1.08f, baseScale.y * 0.94f, baseScale.z * 1.08f);

      float elapsed = 0f;
      const float windupDuration = 0.12f;
      while (elapsed < windupDuration) {
        float t = elapsed / windupDuration;
        transform.rotation = Quaternion.Slerp(startRotation, punchRotation, t);
        transform.localScale = Vector3.Lerp(baseScale, punchScale, t);
        elapsed += Time.deltaTime;
        yield return null;
      }

      elapsed = 0f;
      const float recoverDuration = 0.18f;
      while (elapsed < recoverDuration) {
        float t = elapsed / recoverDuration;
        transform.localScale = Vector3.Lerp(punchScale, baseScale, t);
        elapsed += Time.deltaTime;
        yield return null;
      }

      transform.localScale = baseScale;
    }

    IEnumerator PlayHitFeedback()
    {
      SetDamageColor(new Color(1f, 0.25f, 0.2f, 1f));
      Vector3 hitScale = new Vector3(baseScale.x * 0.92f, baseScale.y * 0.88f, baseScale.z * 0.92f);

      float elapsed = 0f;
      const float squashDuration = 0.1f;
      while (elapsed < squashDuration) {
        transform.localScale = Vector3.Lerp(baseScale, hitScale, elapsed / squashDuration);
        elapsed += Time.deltaTime;
        yield return null;
      }

      elapsed = 0f;
      const float recoverDuration = 0.2f;
      while (elapsed < recoverDuration) {
        transform.localScale = Vector3.Lerp(hitScale, baseScale, elapsed / recoverDuration);
        elapsed += Time.deltaTime;
        yield return null;
      }

      transform.localScale = baseScale;
      ClearDamageColor();
    }

    IEnumerator PlayHitAnimation(string hitState)
    {
      Anim.CrossFade(hitState, 0.05f);

      yield return new WaitForSeconds(HitAnimationDuration);

      if (!isKnockedDown) {
        Anim.CrossFade("Idle", 0.1f);
      }

      hitAnimationRoutine = null;
    }

    IEnumerator PlayKnockDownSequence()
    {
      if (hitFeedback != null) {
        StopCoroutine(hitFeedback);
        hitFeedback = null;
        transform.localScale = baseScale;
      }

      isKnockedDown = true;
      slowedUntil = Time.time + KnockDownDuration + StandUpDuration;
      rb.linearVelocity = Vector3.zero;
      rb.angularVelocity = Vector3.zero;
      Anim.speed = 1f;
      Anim.CrossFade("KnockDown", 0.05f);
      SetDamageColor(new Color(1f, 0.18f, 0.12f, 1f));

      yield return new WaitForSeconds(KnockDownDuration);

      ClearDamageColor();
      Anim.CrossFade("StandUp", 0.08f);

      yield return new WaitForSeconds(StandUpDuration);

      isKnockedDown = false;
      knockDownRoutine = null;
      Anim.CrossFade("Idle", 0.12f);
    }

    void SetDamageColor(Color color)
    {
      foreach (Renderer currentRenderer in renderers) {
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        currentRenderer.GetPropertyBlock(block);
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        currentRenderer.SetPropertyBlock(block);
      }
    }

    void ClearDamageColor()
    {
      foreach (Renderer currentRenderer in renderers) {
        currentRenderer.SetPropertyBlock(null);
      }
    }
}
