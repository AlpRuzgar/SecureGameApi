using System;
using System.Collections.Generic;
using System.Linq;
using SecureGameApi.Models; // GameScoreSubmissionDto ve InputEventDto için
using PhaserDeterministicRandom; // Deterministik rastgele sayı üretimi için bir kütüphane olduğunu varsayalım

namespace SecureGameApi.GameLogic
{
    public class GameSimulator
    {
        // Oyunun temel konfigürasyonu, game.js'deki 'config' nesnesine karşılık gelir.
        // Bu değerler, hem istemci hem de sunucu tarafında aynı olmalıdır.
        private const int GAME_WIDTH = 560;
        private const int GAME_HEIGHT = 1050;
        private const int JUMP_POWER = 810;
        private const int PLAYER_HORIZONTAL_VELOCITY = 500;
        private const int GRAVITY_Y = 1000;
        private const int PLATFORM_GAP = 150;
        private const double SPACE_THRESHOLD_Y = -(10000 * 0.39); // Y ekseninde yukarı doğru negatif artar
        private const double VICTORY_Y = SPACE_THRESHOLD_Y - 8400;
        private const int ENEMY_GAP = 900;
        private const int COIN_GAP = 100;
        private const int BULLET_SPEED = 300;
        private const int BULLET_MAX_LIFE = 4000; // milliseconds
        private const int PLATFORM_WIDTH = 120; // Original image width for scaling
        private const int PLATFORM_HEIGHT = 42; // Original image height for scaling

        // Simülasyonun mevcut durumu
        private PlayerState _player;
        private List<PlatformState> _platforms;
        private List<CoinState> _coins;
        private List<EnemyState> _enemies;
        private List<BulletState> _bullets;
        private TrophyState _trophy;

        private GameMetrics _simulationMetrics;
        private DeterministicRandom _rnd; // Deterministik rastgele sayı üreteci

        // Simülasyon sırasında kullanılacak özel bir zamanlayıcı veya adım sayacı
        private long _currentTime; // milisaniye cinsinden simülasyon içi zaman

        // Oyunun başlangıç anında verilen rastgele tohum
        private long _seed;

        public GameSimulator()
        {
            // Constructor'da herhangi bir özel başlangıç durumu tanımlanabilir
        }

        public ReplayResult Simulate(GameScoreSubmissionDto submission)
        {
            _seed = submission.StartTime; // Oyuncunun gönderdiği startTime'ı tohum olarak kullan
            _rnd = new DeterministicRandom(_seed); // Deterministik rastgele üreteci tohumla

            InitializeSimulationState(submission);

            // Input log'u sırala (zaman damgalarına göre)
            var sortedInputLog = submission.InputLog.OrderBy(e => e.T).ToList();

            // Simülasyon döngüsü
            // Her bir input event'ini işleyene kadar veya oyun sonlanana kadar devam et
            int inputIndex = 0;
            _currentTime = 0; // Simülasyon zamanını sıfırla

            // Oyunun başlangıçta aktif olduğunu varsayalım
            bool gameOver = false;
            bool gameWon = false;

            while (!gameOver && !gameWon && (_currentTime <= submission.DurationSeconds * 1000 + 2000)) // +2 saniye tolerans
            {
                // Bir sonraki input event'inin zamanına kadar simülasyonu ilerlet
                long nextInputTime = long.MaxValue;
                if (inputIndex < sortedInputLog.Count)
                {
                    nextInputTime = sortedInputLog[inputIndex].T;
                }

                // Mevcut frame'in süresi
                // Burada sabit bir "tick" veya "frame rate" kullanmak önemlidir.
                // Örneğin, her 16ms'de bir (yaklaşık 60 FPS) simülasyonu ilerletebiliriz.
                // Phaser'ın update döngüsü FPS'e bağlıdır. Sunucu tarafında bu adımı taklit etmek için
                // küçük, sabit zaman adımları (fixed time step) kullanmak en güvenlisidir.
                const int fixedDeltaTimeMs = 16; // Yaklaşık 60 FPS

                long simulationStepEndTime = Math.Min(nextInputTime, _currentTime + fixedDeltaTimeMs);

                // Eğer bir sonraki input event'ine kadar adım atılacaksa
                while (_currentTime < simulationStepEndTime)
                {
                    UpdateGameLogic(fixedDeltaTimeMs / 1000.0); // Delta time saniye cinsinden
                    _currentTime += fixedDeltaTimeMs;

                    CheckGameOverConditions(); // Her adımda gameOver/gameWon kontrolü
                    if (_simulationMetrics.Hearts <= 0 || _player.Y > _simulationMetrics.MaxHeight + GAME_HEIGHT / 2 + 50)
                    {
                        gameOver = true;
                        break;
                    }
                    if (_simulationMetrics.TrophyCollected)
                    {
                        gameWon = true;
                        break;
                    }
                }

                if (gameOver || gameWon) break;

                // Input event'lerini işle
                while (inputIndex < sortedInputLog.Count && sortedInputLog[inputIndex].T <= _currentTime)
                {
                    ProcessInput(sortedInputLog[inputIndex]);
                    inputIndex++;
                }

                CheckGameOverConditions(); // Input sonrası da kontrol
                if (_simulationMetrics.Hearts <= 0 || _player.Y > _simulationMetrics.MaxHeight + GAME_HEIGHT / 2 + 50)
                {
                    gameOver = true;
                    break;
                }
                if (_simulationMetrics.TrophyCollected)
                {
                    gameWon = true;
                    break;
                }
            }

            // Simülasyon bittikten sonra sonuçları döndür
            return new ReplayResult
            {
                Score = _simulationMetrics.Score,
                CoinCollected = _simulationMetrics.CoinCollected,
                PlatformSpawned = _simulationMetrics.PlatformSpawned,
                TrophyCollected = _simulationMetrics.TrophyCollected,
                // Diğer metrikleri buraya ekleyin
            };
        }

        private void InitializeSimulationState(GameScoreSubmissionDto submission)
        {
            _player = new PlayerState
            {
                X = GAME_WIDTH / 2,
                Y = GAME_HEIGHT - 400,
                VelocityY = 0,
                VelocityX = 0,
                Scale = 0.1,
                DisplayWidth = 0.1 * 500, // Player image original size * scale
                DisplayHeight = 0.1 * 500,
                BodyWidth = 0.1 * 500 * 0.7, // Assuming 70% of display width for body
                BodyHeight = 0.1 * 500 * 0.7 // Assuming 70% of display height for body
            };

            // Başlangıç zemini oluştur
            _platforms = new List<PlatformState>();
            // Trambolin için özel bir platform değil, sadece collision için bir ground objesi ekliyoruz.
            _platforms.Add(new PlatformState
            {
                Type = PlatformType.Ground,
                X = GAME_WIDTH / 2,
                Y = GAME_HEIGHT - 320,
                Width = GAME_WIDTH,
                Height = 20,
                IsImmovable = true,
                BodyWidth = GAME_WIDTH,
                BodyHeight = 20
            });

            _coins = new List<CoinState>();
            _enemies = new List<EnemyState>();
            _bullets = new List<BulletState>();
            _trophy = null; // Başlangıçta trophy yok

            _simulationMetrics = new GameMetrics
            {
                PlayerId = submission.PlayerId,
                StartTime = submission.StartTime,
                EndTime = submission.EndTime, // Bu simülasyonun kendi endTime'ı olacak
                PlatformSpawned = 0,
                EnemySpawned = 0,
                CoinSpawned = 0,
                CoinCollected = 0,
                TrophySpawned = false,
                TrophyCollected = false,
                DamageTaken = 0,
                JumpCount = 0,
                Score = 0,
                Hearts = 6, // Başlangıç kalp sayısı
                MaxHeight = _player.Y // En yüksek nokta (ekranda en alttan başlar, y değeri azalır)
            };

            // Oyunun kendi içindeki rastgelelik tohumlamasını simüle et
            // Bu kısım kritik: C# tarafında da aynı deterministik RNG'yi kullanmalıyız.
            _rnd = new DeterministicRandom(submission.StartTime);

            // Oyunun lastPlatformY, lastEnemyY, lastCoinY gibi spawn noktalarını senkronize et.
            _simulationMetrics.LastPlatformY = GAME_HEIGHT - 320; // Ground Y
            _simulationMetrics.LastCoinY = 650;
            _simulationMetrics.LastEnemyY = 0; // Bu değer GameScene'deki initial değeri ile aynı olmalı
        }

        private void UpdateGameLogic(double deltaTime)
        {
            // Fizik güncellemeleri
            _player.VelocityY += GRAVITY_Y * deltaTime;
            _player.Y += _player.VelocityY * deltaTime;
            _player.X += _player.VelocityX * deltaTime;

            // Oyuncuyu ekran sınırları içinde döndür
            if (_player.X < 0) _player.X = GAME_WIDTH;
            else if (_player.X > GAME_WIDTH) _player.X = 0;

            // MaxHeight güncellemesi
            if (_player.Y < _simulationMetrics.MaxHeight)
            {
                _simulationMetrics.MaxHeight = _player.Y;
            }

            // Platform çarpışmalarını kontrol et
            HandlePlatformCollisions();

            // Coin çarpışmalarını kontrol et
            HandleCoinCollisions();

            // Düşman ve mermi çarpışmalarını kontrol et
            HandleEnemyAndBulletCollisions();

            // Mermileri hareket ettir ve ömürlerini kontrol et
            UpdateBullets(deltaTime);

            // Hareketli platformları hareket ettir
            UpdateMovingPlatforms(deltaTime);

            // Düşmanları hareket ettir ve mermi attır
            UpdateEnemies(deltaTime);

            // Yeni platformları, düşmanları ve coinleri spawn et
            SpawnGameElements();

            // Trophy kontrolü
            if (!_simulationMetrics.TrophySpawned && _player.Y < VICTORY_Y + 1000)
            {
                AddTrophy();
                _simulationMetrics.TrophySpawned = true;
            }

            // Kazanma koşulu
            if (_trophy != null && _player.Intersects(_trophy))
            {
                _simulationMetrics.TrophyCollected = true;
            }
        }

        private void CheckGameOverConditions()
        {
            // Düşme kontrolü (GameScene'deki handleFall metodu)
            if (_player.Y > _simulationMetrics.MaxHeight + GAME_HEIGHT / 2 + 50)
            {
                _simulationMetrics.Hearts = 0; // Can bitmese bile düşme game over sayılır
            }

            // Can kontrolü
            if (_simulationMetrics.Hearts <= 0)
            {
                // Game Over
            }
        }

        private void ProcessInput(InputEventDto input)
        {
            // Klavye girdilerini işle
            if (input.Type == "keydown")
            {
                if (input.Key == "ArrowLeft")
                {
                    _player.VelocityX = -PLAYER_HORIZONTAL_VELOCITY;
                }
                else if (input.Key == "ArrowRight")
                {
                    _player.VelocityX = PLAYER_HORIZONTAL_VELOCITY;
                }
            }
            else if (input.Type == "keyup")
            {
                if (input.Key == "ArrowLeft" || input.Key == "ArrowRight")
                {
                    _player.VelocityX = 0;
                }
            }
            // Mobil dokunmatik girdileri işle (PointerDown / PointerUp)
            else if (input.Type == "pointerdown")
            {
                // Phaser'daki handleMobileControls'a benzer mantık
                // Mobil için sadece yatay hareket
                if (input.X.HasValue)
                {
                    if (input.X.Value > GAME_WIDTH / 2)
                    {
                        _player.VelocityX = PLAYER_HORIZONTAL_VELOCITY;
                    }
                    else
                    {
                        _player.VelocityX = -PLAYER_HORIZONTAL_VELOCITY;
                    }
                }
            }
            else if (input.Type == "pointerup")
            {
                _player.VelocityX = 0;
            }
        }

        private void HandlePlatformCollisions()
        {
            // Phaser'daki onlyTopCollision mantığını taklit et
            foreach (var platform in _platforms)
            {
                // Eğer oyuncu düşüyorsa ve platformun üzerine çarpıyorsa
                if (_player.VelocityY >= 0 && _player.IntersectsTop(platform))
                {
                    // Oyuncunun platforma zıplamasını simüle et
                    int jumpPower = JUMP_POWER;
                    if (platform.Type == PlatformType.Ground)
                    {
                        jumpPower += 200; // Başlangıç zemini için ekstra zıplama
                    }
                    _player.VelocityY = -jumpPower;
                    _simulationMetrics.JumpCount++;

                    // Kırılan platform ise
                    if (platform.Type == PlatformType.Breaking)
                    {
                        // Kırılma animasyonunu simüle et ve platformu kaldır
                        _platforms.Remove(platform);
                        break; // Listeyi değiştirdiğimiz için döngüden çık
                    }
                }
            }
        }

        private void HandleCoinCollisions()
        {
            for (int i = _coins.Count - 1; i >= 0; i--)
            {
                var coin = _coins[i];
                if (_player.Intersects(coin))
                {
                    _coins.RemoveAt(i);
                    _simulationMetrics.Score += 10;
                    _simulationMetrics.CoinCollected++;
                }
            }
        }

        private void HandleEnemyAndBulletCollisions()
        {
            // Oyuncu-Düşman çarpışması
            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                var enemy = _enemies[i];
                if (_player.Intersects(enemy))
                {
                    // Phaser'daki hasar mantığına göre 3 kalp götür
                    int damageAmount = Math.Min(3, _simulationMetrics.Hearts);
                    _simulationMetrics.Hearts -= damageAmount;
                    _simulationMetrics.DamageTaken += damageAmount;
                    _enemies.RemoveAt(i); // Düşmanı kaldır
                    break;
                }
            }

            // Oyuncu-Mermi çarpışması
            for (int i = _bullets.Count - 1; i >= 0; i--)
            {
                var bullet = _bullets[i];
                if (_player.Intersects(bullet))
                {
                    _simulationMetrics.Hearts -= 1;
                    _simulationMetrics.DamageTaken += 1;
                    _bullets.RemoveAt(i); // Mermiyi kaldır
                    break;
                }
            }
        }

        private void UpdateBullets(double deltaTime)
        {
            for (int i = _bullets.Count - 1; i >= 0; i--)
            {
                var bullet = _bullets[i];
                bullet.X += bullet.VelocityX * deltaTime;
                bullet.Y += bullet.VelocityY * deltaTime;
                bullet.LifeTime += (long)(deltaTime * 1000); // Milisaniye cinsinden artır

                if (bullet.LifeTime > BULLET_MAX_LIFE)
                {
                    _bullets.RemoveAt(i);
                }
            }
        }

        private void UpdateMovingPlatforms(double deltaTime)
        {
            foreach (var platform in _platforms.Where(p => p.Type == PlatformType.Moving))
            {
                // Phaser tween mantığını simüle et
                // Bu kısım biraz karmaşık olabilir çünkü tween'ler doğrusal değildir.
                // Basit bir sinüs dalgası hareketi yeterli olabilir.
                // Game.js'de tween'lerin süresi ve mesafesi rastgele seçiliyor.
                // Bu rastgelelik de DeterministicRandom ile kontrol edilmeli.

                // Eğer platformun tween bilgileri yoksa başlat
                if (!platform.TweenInfoInitialized)
                {
                    platform.InitialX = platform.X;
                    platform.MoveDistance = _rnd.Between(100, 200);
                    platform.MoveDuration = _rnd.Between(1500, 2500); // ms
                    platform.TweenDirection = (_rnd.NextDouble() > 0.5) ? 1 : -1;
                    platform.TweenProgress = 0;
                    platform.TweenInfoInitialized = true;
                }

                platform.TweenProgress += deltaTime;
                double progress = platform.TweenProgress / (platform.MoveDuration / 1000.0); // Normalleştirilmiş ilerleme [0, 1]

                // Sinüs dalgası yoyo hareketi için
                double sineValue = Math.Sin(progress * Math.PI); // [0, 1] -> [0, 0]
                platform.X = platform.InitialX + platform.MoveDistance * platform.TweenDirection * sineValue;

                // Tween bitince (yoyo) yön değiştir ve resetle
                if (progress >= 1.0)
                {
                    platform.TweenProgress = 0;
                    platform.TweenDirection *= -1; // Yön değiştir
                }
            }
        }

        private void UpdateEnemies(double deltaTime)
        {
            foreach (var enemy in _enemies)
            {
                // Düşman tipine göre hareket ve eylem
                if (enemy.Type == EnemyType.Bird)
                {
                    // Basit yatay hareket ve yoyo
                    if (!enemy.TweenInfoInitialized)
                    {
                        enemy.InitialX = enemy.X;
                        enemy.InitialY = enemy.Y;
                        enemy.MoveDistance = _rnd.Between(100, 200);
                        enemy.MoveDuration = 2000; // ms
                        enemy.TweenDirection = 1;
                        enemy.TweenProgress = 0;
                        enemy.TweenInfoInitialized = true;
                    }

                    enemy.TweenProgress += deltaTime;
                    double progress = enemy.TweenProgress / (enemy.MoveDuration / 1000.0);
                    double sineValue = Math.Sin(progress * Math.PI);

                    enemy.X = enemy.InitialX + enemy.MoveDistance * enemy.TweenDirection * sineValue;
                    enemy.Y = enemy.InitialY - 20 * sineValue; // Hafif dikey hareket
                    if (progress >= 1.0)
                    {
                        enemy.TweenProgress = 0;
                        enemy.TweenDirection *= -1;
                    }
                }
                else if (enemy.Type == EnemyType.UFO)
                {
                    // Hafif titreme hareketi
                    if (!enemy.TweenInfoInitialized)
                    {
                        enemy.InitialX = enemy.X;
                        enemy.InitialY = enemy.Y;
                        enemy.MoveDistance = _rnd.Between(-40, 40);
                        enemy.Angle = 10;
                        enemy.MoveDuration = 2000; // ms
                        enemy.TweenProgress = 0;
                        enemy.TweenInfoInitialized = true;
                    }

                    enemy.TweenProgress += deltaTime;
                    double progress = enemy.TweenProgress / (enemy.MoveDuration / 1000.0);
                    double sineValue = Math.Sin(progress * Math.PI);

                    enemy.X = enemy.InitialX + enemy.MoveDistance * sineValue;
                    enemy.Y = enemy.InitialY - 10 * sineValue;
                    enemy.Rotation = enemy.Angle * sineValue;

                    if (progress >= 1.0)
                    {
                        enemy.TweenProgress = 0;
                    }
                }
                else if (enemy.Type == EnemyType.Alien)
                {
                    // Hafif dikey hareket ve dönme
                    if (!enemy.TweenInfoInitialized)
                    {
                        enemy.InitialX = enemy.X;
                        enemy.InitialY = enemy.Y;
                        enemy.Angle = _rnd.Between(-5, 5);
                        enemy.MoveDuration = 1500; // ms
                        enemy.TweenProgress = 0;
                        enemy.TweenInfoInitialized = true;
                    }

                    enemy.TweenProgress += deltaTime;
                    double progress = enemy.TweenProgress / (enemy.MoveDuration / 1000.0);
                    double sineValue = Math.Sin(progress * Math.PI);

                    enemy.Y = enemy.InitialY - 10 * sineValue;
                    enemy.Rotation = enemy.Angle * sineValue;

                    if (progress >= 1.0)
                    {
                        enemy.TweenProgress = 0;
                    }

                    // Mermi atma mantığı (her 2 saniyede bir)
                    enemy.ShootTimer += (long)(deltaTime * 1000);
                    if (enemy.ShootTimer >= 2000)
                    {
                        enemy.ShootTimer = 0; // Reset timer
                        double distance = Math.Sqrt(Math.Pow(enemy.X - _player.X, 2) + Math.Pow(enemy.Y - _player.Y, 2));
                        if (distance < 800)
                        {
                            AddBullet(enemy.X, enemy.Y, _player.X, _player.Y);
                        }
                    }
                }
            }
        }

        private void SpawnGameElements()
        {
            double spawnHighPoint = _player.Y - GAME_HEIGHT / 2;

            // Platform spawn
            if (_simulationMetrics.LastPlatformY > spawnHighPoint && _simulationMetrics.LastPlatformY > VICTORY_Y)
            {
                PlatformType type;
                int platformMod = _simulationMetrics.PlatformSpawned % 5;
                switch (platformMod)
                {
                    case 0:
                    case 1:
                    case 3:
                        type = PlatformType.Normal;
                        break;
                    case 2:
                        type = PlatformType.Moving;
                        break;
                    case 4:
                        type = PlatformType.Breaking;
                        break;
                    default:
                        type = PlatformType.Normal; // Should not happen
                        break;
                }
                AddPlatform(type);
            }

            // Düşman spawn
            // Phaser'daki inSpace kontrolünü simüle et
            bool inSpace = _player.Y < SPACE_THRESHOLD_Y;
            if (_simulationMetrics.LastEnemyY > spawnHighPoint + GAME_HEIGHT / 2 && _simulationMetrics.LastEnemyY > VICTORY_Y + 1200)
            {
                EnemyType type;
                if (!inSpace)
                {
                    type = EnemyType.Bird;
                }
                else
                {
                    int enemyMod = _simulationMetrics.EnemySpawned % 2;
                    type = (enemyMod == 0) ? EnemyType.UFO : EnemyType.Alien;
                }
                AddEnemy(type);
            }

            // Coin spawn
            if (_simulationMetrics.LastCoinY > spawnHighPoint && _simulationMetrics.LastCoinY > VICTORY_Y + 800)
            {
                AddCoin();
            }
        }

        private void AddPlatform(PlatformType type)
        {
            double y = _simulationMetrics.LastPlatformY - PLATFORM_GAP;
            double x = _rnd.Between(0, GAME_WIDTH);

            // Platformun gerçek boyutları ve scale değerleri GameScene'den alınmalı
            double displayWidth = PLATFORM_WIDTH * (120.0 / PLATFORM_WIDTH); // Scale factor from game.js
            double displayHeight = PLATFORM_HEIGHT * (42.0 / PLATFORM_HEIGHT);

            _platforms.Add(new PlatformState
            {
                Type = type,
                X = x,
                Y = y,
                Width = displayWidth,
                Height = displayHeight,
                IsImmovable = true,
                BodyWidth = displayWidth * 0.8, // 0.8 scale from game.js
                BodyHeight = displayHeight * 0.1 // 0.1 scale from game.js
            });
            _simulationMetrics.LastPlatformY = y;
            _simulationMetrics.PlatformSpawned++;
        }

        private void AddCoin()
        {
            double y = _simulationMetrics.LastCoinY - COIN_GAP;
            double x = _rnd.Between(100, GAME_WIDTH - 100);

            _coins.Add(new CoinState
            {
                X = x,
                Y = y,
                Width = 0.1 * 300, // Coin original size * scale
                Height = 0.1 * 300,
                BodyWidth = 0.1 * 300,
                BodyHeight = 0.1 * 300
            });
            _simulationMetrics.LastCoinY = y;
            _simulationMetrics.CoinSpawned++;
        }

        private void AddEnemy(EnemyType type)
        {
            double y = _simulationMetrics.LastEnemyY - ENEMY_GAP;
            double x = _rnd.Between(100, GAME_WIDTH - 100);

            _enemies.Add(new EnemyState
            {
                Type = type,
                X = x,
                Y = y,
                Width = 1.2 * 100, // Bird original size * scale
                Height = 1.2 * 100,
                BodyWidth = 1.2 * 100,
                BodyHeight = 1.2 * 100,
                ShootTimer = 0 // Alien için mermi atma sayacı
            });
            _simulationMetrics.LastEnemyY = y;
            _simulationMetrics.EnemySpawned++;
        }

        private void AddBullet(double startX, double startY, double targetX, double targetY)
        {
            double angle = Math.Atan2(targetY - startY, targetX - startX);
            _bullets.Add(new BulletState
            {
                X = startX,
                Y = startY,
                VelocityX = Math.Cos(angle) * BULLET_SPEED,
                VelocityY = Math.Sin(angle) * BULLET_SPEED,
                LifeTime = 0,
                Width = 1.0 * 50, // Bullet original size * scale
                Height = 1.0 * 50,
                BodyWidth = 1.0 * 50,
                BodyHeight = 1.0 * 50
            });
        }

        private void AddTrophy()
        {
            _trophy = new TrophyState
            {
                X = GAME_WIDTH / 2,
                Y = VICTORY_Y,
                Width = 0.2 * 300, // Trophy original size * scale
                Height = 0.2 * 300,
                BodyWidth = 0.2 * 300 * 0.2, // From game.js body.setSize
                BodyHeight = 0.2 * 300 * 0.2
            };
        }
    }

    // Yardımcı Durum Sınıfları (Basitleştirilmiş)
    // Bunlar, oyun nesnelerinin mevcut fiziksel durumunu temsil eder.
    // Phaser'ın body.x, body.y, body.width, body.height gibi özelliklerine karşılık gelir.
    public abstract class GameObjectState
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; } // Tam görsel genişlik
        public double Height { get; set; } // Tam görsel yükseklik
        public double BodyWidth { get; set; } // Çarpışma kutusu genişliği
        public double BodyHeight { get; set; } // Çarpışma kutusu yüksekliği

        // Basit AABB (Axis-Aligned Bounding Box) çarpışma tespiti
        public bool Intersects(GameObjectState other)
        {
            double thisLeft = X - BodyWidth / 2;
            double thisRight = X + BodyWidth / 2;
            double thisTop = Y - BodyHeight / 2;
            double thisBottom = Y + BodyHeight / 2;

            double otherLeft = other.X - other.BodyWidth / 2;
            double otherRight = other.X + other.BodyWidth / 2;
            double otherTop = other.Y - other.BodyHeight / 2;
            double otherBottom = other.Y + other.BodyHeight / 2;

            return !(thisLeft >= otherRight ||
                     thisRight <= otherLeft ||
                     thisTop >= otherBottom ||
                     thisBottom <= otherTop);
        }

        // Sadece üstten çarpışma (oyuncu platforma düşerken)
        public bool IntersectsTop(GameObjectState other)
        {
            // Oyuncunun altı platformun üstüne yakın mı?
            bool playerFallingOntoPlatform = (Y + BodyHeight / 2) >= (other.Y - other.BodyHeight / 2) - 5 && // 5 piksel tolerans
                                            (Y + BodyHeight / 2) <= (other.Y - other.BodyHeight / 2) + 5;

            return Intersects(other) && playerFallingOntoPlatform;
        }
    }

    public class PlayerState : GameObjectState
    {
        public double VelocityX { get; set; }
        public double VelocityY { get; set; }
        public double Scale { get; set; } // Player scale (0.1 in game.js)
        public double DisplayWidth { get; set; } // Görsel genişlik
        public double DisplayHeight { get; set; } // Görsel yükseklik
    }

    public enum PlatformType { Normal, Moving, Breaking, Ground }
    public class PlatformState : GameObjectState
    {
        public PlatformType Type { get; set; }
        public bool IsImmovable { get; set; }
        // Hareketli platformlar için tween bilgileri
        public bool TweenInfoInitialized { get; set; } = false;
        public double InitialX { get; set; }
        public double MoveDistance { get; set; }
        public int MoveDuration { get; set; } // ms
        public int TweenDirection { get; set; }
        public double TweenProgress { get; set; } // normalized [0,1]
        public double Rotation { get; set; } // UFO gibi objeler için
        public double Angle { get; set; } // UFO gibi objeler için
    }

    public class CoinState : GameObjectState { }

    public enum EnemyType { Bird, UFO, Alien }
    public class EnemyState : GameObjectState
    {
        public EnemyType Type { get; set; }
        public long ShootTimer { get; set; } // Alien için mermi atma sayacı
        public bool TweenInfoInitialized { get; set; } = false;
        public double InitialX { get; set; }
        public double InitialY { get; set; }
        public double MoveDistance { get; set; }
        public int MoveDuration { get; set; } // ms
        public int TweenDirection { get; set; }
        public double TweenProgress { get; set; } // normalized [0,1]
        public double Rotation { get; set; }
        public double Angle { get; set; }
    }

    public class BulletState : GameObjectState
    {
        public double VelocityX { get; set; }
        public double VelocityY { get; set; }
        public long LifeTime { get; set; } // ms
    }

    public class TrophyState : GameObjectState { }

    // Oyun metriklerini tutmak için sınıf
    public class GameMetrics
    {
        public string PlayerId { get; set; }
        public long StartTime { get; set; }
        public long EndTime { get; set; }
        public int PlatformSpawned { get; set; }
        public int EnemySpawned { get; set; }
        public int CoinSpawned { get; set; }
        public int CoinCollected { get; set; }
        public bool TrophySpawned { get; set; }
        public bool TrophyCollected { get; set; }
        public int DamageTaken { get; set; }
        public int JumpCount { get; set; }
        public int Score { get; set; }
        public int Hearts { get; set; }
        public double MaxHeight { get; set; } // En yüksek ulaşılan Y koordinatı (oyun metriklerinde 'heightReached' olarak kullanılacak)

        // Spawn konumları için ek alanlar (bu değerler sürekli değişir ve simülasyonun kendi içinde güncellenir)
        public double LastPlatformY { get; set; }
        public double LastCoinY { get; set; }
        public double LastEnemyY { get; set; }
    }
}

namespace PhaserDeterministicRandom
{
    // Bu, Phaser'ın RND nesnesini taklit eden basitleştirilmiş bir deterministik rastgele sayı üreteci.
    // Phaser'ın kullandığı LCG algoritmasının C# karşılığı olmalıdır.
    // Gerçek Phaser koduna bakıp daha doğru bir implementasyon yapmak gerekebilir.
    // Şimdilik System.Random'ı tohumlayarak kullanıyorum, ancak bu tam olarak deterministik olmayabilir
    // farklı .NET versiyonlarında. Daha sağlam bir çözüm için LCG'yi kendiniz implemente etmelisiniz.
    public class DeterministicRandom
    {
        private Random _random;

        public DeterministicRandom(long seed)
        {
            _random = new Random((int)seed); // System.Random için int tohumlama
        }

        public double NextDouble()
        {
            return _random.NextDouble();
        }

        public int Next(int minValue, int maxValue)
        {
            // maxValue exclusive, yani maxValue'den küçük
            return _random.Next(minValue, maxValue);
        }

        public int Between(int minValue, int maxValue)
        {
            // Phaser'ın Between metodu inclusive olabilir, yani maxValue dahil.
            // System.Random.Next(min, max) ise max exclusive.
            // Bu yüzden maxValue + 1 yapabiliriz eğer Phaser'daki gibi inclusive ise.
            return _random.Next(minValue, maxValue + 1);
        }
    }
}