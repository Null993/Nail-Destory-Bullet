using System;
using GlobalEnums;
using Modding;
using On;
using Satchel.BetterMenus;
using UnityEngine;
using Logger = Modding.Logger;

namespace DestroyBulletOnAttackMod
{
    // 可保存的全局设置
    public class GlobalSettings
    {
        public bool Enabled = true;
        public bool DebugLog = false;
        public bool LogAllCollisions = false;
        public bool DestroyDamageHeroObjects = false; // 修改：默认关闭实验性功能
    }

    public class DestroyBulletOnAttack
        : Mod
        , IGlobalSettings<GlobalSettings>
        , ICustomMenuMod
        , ITogglableMod
    {
        internal static DestroyBulletOnAttack Instance { get; private set; }
        internal static GlobalSettings GS { get; private set; } = new GlobalSettings();

        public DestroyBulletOnAttack() : base("Destroy Bullet On Attack")
        {
            Instance = this;
        }

        public override string GetVersion() => "1.2.3"; // 更新版本号

        // ===== IGlobalSettings 接口实现 =====
        public void OnLoadGlobal(GlobalSettings settings)
        {
            GS = settings ?? new GlobalSettings();
        }

        public GlobalSettings OnSaveGlobal()
        {
            return GS;
        }

        // ===== Mod 初始化 / 卸载 =====
        public override void Initialize()
        {
            Log("[DestroyBulletOnAttack] Initializing");
            On.NailSlash.Awake += NailSlash_Awake;
            Log("[DestroyBulletOnAttack] Initialized");
        }

        public void Unload()
        {
            On.NailSlash.Awake -= NailSlash_Awake;

            var allSlash = GameObject.FindObjectsOfType<NailSlash>();
            foreach (var s in allSlash)
            {
                var comp = s.gameObject.GetComponent<NailBulletDestroyer>();
                if (comp != null) UnityEngine.Object.Destroy(comp);
            }

            Log("[DestroyBulletOnAttack] Unloaded");
        }

        // ===== 把检测器挂到每个 Slash 上 =====
        private void NailSlash_Awake(On.NailSlash.orig_Awake orig, NailSlash self)
        {
            orig(self);
            TryAttachDestroyer(self);
        }

        internal static void TryAttachDestroyer(NailSlash slash)
        {
            if (slash == null) return;

            var col = slash.GetComponent<Collider2D>();
            if (col == null)
            {
                if (GS.DebugLog) Logger.Log("[DestroyBulletOnAttack] Slash has no Collider2D: " + slash.name);
                return;
            }

            var existing = slash.gameObject.GetComponent<NailBulletDestroyer>();
            if (existing == null)
            {
                var comp = slash.gameObject.AddComponent<NailBulletDestroyer>();
                comp.SetSource(slash);
                if (GS.DebugLog) Logger.Log("[DestroyBulletOnAttack] Attached NailBulletDestroyer to " + slash.name);
            }
        }

        // ===== 菜单 =====
        private Menu menuRef;
        public bool ToggleButtonInsideMenu => true;

        public MenuScreen GetMenuScreen(MenuScreen modListMenu, ModToggleDelegates? toggles)
        {
            if (menuRef == null)
            {
                menuRef = new Menu("Destroy Bullet On Attack", new Element[]
                {
                    Blueprints.CreateToggle(
                        toggles.Value,
                        "Mod Enabled",
                        "Allow all nail attacks to destroy enemy bullets"
                    ),
                    new HorizontalOption(
                        "Debug Log", "",
                        new string[] {"OFF","ON"},
                        (i) => GS.DebugLog = (i == 1),
                        () => GS.DebugLog ? 1 : 0
                    ),
                    new HorizontalOption(
                        "Log All Collisions", "Log all objects that collide with nail",
                        new string[] {"OFF","ON"},
                        (i) => GS.LogAllCollisions = (i == 1),
                        () => GS.LogAllCollisions ? 1 : 0
                    ),
                    // 修改：添加实验性功能标签和警告
                    new TextPanel(
                        "Experimental Features:",
                        fontSize: 40
                    ),
                    new HorizontalOption(
                        "Destroy Non-Standard Projectiles",
                        "WARNING: Experimental! May affect boss weapons and other objects",
                        new string[] {"OFF","ON"},
                        (i) => GS.DestroyDamageHeroObjects = (i == 1),
                        () => GS.DestroyDamageHeroObjects ? 1 : 0
                    )
                });
            }
            return menuRef.GetMenuScreen(modListMenu);
        }

        // ===== 小工具 =====
        internal static void Log(string msg)
        {
            if (GS.DebugLog) Logger.Log(msg);
        }

        internal static void LogCollisionDetails(GameObject collidedObject, Collider2D collider)
        {
            if (!GS.LogAllCollisions) return;

            try
            {
                string logMessage = $"[CollisionDebug] Object: {collidedObject.name}\n" +
                                   $" - Tag: {collidedObject.tag}\n" +
                                   $" - Layer: {LayerMask.LayerToName(collidedObject.layer)}\n" +
                                   $" - Position: {collidedObject.transform.position}\n" +
                                   $" - Active: {collidedObject.activeInHierarchy}\n" +
                                   $" - Collider: {collider?.GetType().Name}\n";

                var enemyBullet = collidedObject.GetComponent<EnemyBullet>();
                var damageHero = collidedObject.GetComponent<DamageHero>();
                var rigidbody = collidedObject.GetComponent<Rigidbody2D>();
                var renderer = collidedObject.GetComponent<Renderer>();

                if (enemyBullet != null) logMessage += $" - EnemyBullet: YES\n";
                if (damageHero != null) logMessage += $" - DamageHero: {damageHero.damageDealt} damage\n";
                if (rigidbody != null) logMessage += $" - Rigidbody2D: {rigidbody.bodyType}, Velocity: {rigidbody.velocity}\n";
                if (renderer != null) logMessage += $" - Renderer: {renderer.GetType().Name}, Visible: {renderer.isVisible}\n";

                if (collidedObject.transform.parent != null)
                {
                    logMessage += $" - Parent: {collidedObject.transform.parent.name}\n";
                    var parentEnemyBullet = collidedObject.transform.parent.GetComponent<EnemyBullet>();
                    if (parentEnemyBullet != null) logMessage += $" - Parent has EnemyBullet: YES\n";
                }

                if (collidedObject.transform.childCount > 0)
                {
                    logMessage += $" - Children: {collidedObject.transform.childCount}\n";
                }

                // 添加对象类型信息，帮助调试
                if (damageHero != null && enemyBullet == null)
                {
                    logMessage += $" - Type: Non-Standard Projectile (DamageHero only)\n";
                    logMessage += $" - Experimental Feature: {GS.DestroyDamageHeroObjects}\n";
                }

                Logger.Log(logMessage);
            }
            catch (Exception e)
            {
                Logger.Log($"[CollisionDebug] Error logging collision details: {e.Message}");
            }
        }
    }

    public class NailBulletDestroyer : MonoBehaviour
    {
        private NailSlash slash;
        private Collider2D slashCollider;
        private readonly Collider2D[] hits = new Collider2D[24];
        private ContactFilter2D filter;
        private Transform heroT;

        private readonly System.Collections.Generic.HashSet<GameObject> processedObjects = new System.Collections.Generic.HashSet<GameObject>();

        public void SetSource(NailSlash source) => slash = source;

        private void Awake()
        {
            slashCollider = GetComponent<Collider2D>();
            heroT = HeroController.instance != null ? HeroController.instance.transform : null;

            filter = new ContactFilter2D
            {
                useTriggers = true,
                useLayerMask = false
            };
        }

        private void FixedUpdate()
        {
            if (!DestroyBulletOnAttack.GS.Enabled) return;
            if (slashCollider == null || !slashCollider.enabled || !gameObject.activeInHierarchy) return;

            processedObjects.Clear();

            int count = slashCollider.OverlapCollider(filter, hits);
            if (count <= 0) return;

            for (int i = 0; i < count; i++)
            {
                var other = hits[i];
                if (other == null || processedObjects.Contains(other.gameObject)) continue;

                // 记录所有碰撞对象
                DestroyBulletOnAttack.LogCollisionDetails(other.gameObject, other);

                // 尝试处理各种类型的子弹
                if (TryHandleBullet(other.gameObject))
                {
                    processedObjects.Add(other.gameObject);
                }
            }
        }

        private bool TryHandleBullet(GameObject target)
        {
            // 1. 标准 EnemyBullet 组件（优先处理）
            var enemyBullet = target.GetComponent<EnemyBullet>();
            if (enemyBullet != null)
            {
                return HandleStandardBullet(enemyBullet);
            }

            // 2. 父物体中的 EnemyBullet
            var parentEnemyBullet = target.GetComponentInParent<EnemyBullet>();
            if (parentEnemyBullet != null && parentEnemyBullet.gameObject != target)
            {
                return HandleStandardBullet(parentEnemyBullet);
            }

            // 3. 具有DamageHero组件的对象（实验性功能，默认关闭）
            if (DestroyBulletOnAttack.GS.DestroyDamageHeroObjects && IsDamageHeroProjectile(target))
            {
                return HandleDamageHeroProjectile(target);
            }

            return false;
        }

        private bool HandleStandardBullet(EnemyBullet bullet)
        {
            if (heroT == null) return false;

            try
            {
                DestroyBulletOnAttack.Log("[DestroyBulletOnAttack] Destroying standard bullet: " + bullet.name);
                bullet.OrbitShieldHit(heroT);
                return true;
            }
            catch (System.Exception e)
            {
                DestroyBulletOnAttack.Log("[DestroyBulletOnAttack] Error destroying bullet: " + e.Message);
                return false;
            }
        }

        private bool IsDamageHeroProjectile(GameObject obj)
        {
            // 检查是否有DamageHero组件
            var damageHero = obj.GetComponent<DamageHero>();
            if (damageHero == null) return false;

            // 检查是否在攻击相关的层级（根据日志分析）
            string layerName = LayerMask.LayerToName(obj.layer);
            bool isAttackLayer = layerName.Contains("Attack") || layerName.Contains("Enemy") ||
                               layerName.Contains("Projectile") || obj.layer == LayerMask.NameToLayer("Enemy Attack");

            // 排除玩家自身的对象
            bool isPlayerObject = obj.CompareTag("Player") || obj.CompareTag("HeroBox") ||
                                (obj.transform.parent != null && obj.transform.parent.CompareTag("Player"));

            // 有渲染器且可见（避免处理不可见的对象）
            var renderer = obj.GetComponent<Renderer>();
            bool isVisible = renderer == null || renderer.isVisible;

            return isAttackLayer && !isPlayerObject && isVisible;
        }

        private bool HandleDamageHeroProjectile(GameObject projectile)
        {
            try
            {
                DestroyBulletOnAttack.Log("[DestroyBulletOnAttack] Destroying DamageHero projectile: " + projectile.name);

                var damageHero = projectile.GetComponent<DamageHero>();
                if (damageHero != null)
                {
                    // 禁用伤害组件
                    damageHero.enabled = false;
                }

                // 停止物理运动（如果有）
                var rigidbody = projectile.GetComponent<Rigidbody2D>();
                if (rigidbody != null && rigidbody.bodyType == RigidbodyType2D.Dynamic)
                {
                    rigidbody.velocity = Vector2.zero;
                    rigidbody.angularVelocity = 0f;
                }

                // 禁用碰撞体
                var collider = projectile.GetComponent<Collider2D>();
                if (collider != null)
                {
                    collider.enabled = false;
                }

                // 添加视觉反馈
                var renderer = projectile.GetComponent<Renderer>();
                if (renderer != null)
                {
                    // 改变颜色表示被消除
                    renderer.material.color = new Color(0.7f, 0.7f, 0.7f, 0.5f);
                }

                // 对于EnemyBullet类型的对象，尝试调用标准消除逻辑
                var enemyBulletInParent = projectile.GetComponentInParent<EnemyBullet>();
                if (enemyBulletInParent != null && heroT != null)
                {
                    try
                    {
                        enemyBulletInParent.OrbitShieldHit(heroT);
                    }
                    catch
                    {
                        // 如果标准方法失败，使用延迟销毁
                        UnityEngine.Object.Destroy(projectile, 0.3f);
                    }
                }
                else
                {
                    // 延迟销毁非标准bullet
                    UnityEngine.Object.Destroy(projectile, 0.3f);
                }

                return true;
            }
            catch (System.Exception e)
            {
                DestroyBulletOnAttack.Log("[DestroyBulletOnAttack] Error destroying DamageHero projectile: " + e.Message);
                return false;
            }
        }
    }
}