using System;

namespace 你的命名空间
{
    struct Boss
    {
        public int hp;
        public int atk;
        public int def;
        public int healing;
        public Boss(int hp, int atk, int def, int healing)
        {
            this.hp = hp;
            this.atk = atk;
            this.def = def;
            this.healing = healing;
        }
    }

    struct Player
    {
        public int hp;
        public int atk;
        public int def;
        public int healing;
        public Player(int hp, int atk, int def, int healing)
        {
            this.hp = hp;
            this.atk = atk;
            this.def = def;
            this.healing = healing;
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            // Main里面：创建角色，启动战斗！
            Random rand = new Random();
            Player player = new Player(100, rand.Next(10, 20), rand.Next(3, 8), rand.Next(5, 12));
            Boss boss = new Boss(120, rand.Next(8, 18), rand.Next(2, 7), rand.Next(4, 9));

            Fight(player, boss);
        }

        // Fight放在Program类里面，Main的外面
        static void Fight(Player player, Boss boss)
        {
            Random r = new Random();
            bool isPlayerDef = false; //标记玩家是否防御
            while (player.hp > 0 && boss.hp > 0)
            {
                isPlayerDef = false;
                Console.WriteLine($"\n你的血量: {player.hp}, Boss血量: {boss.hp}");
                Console.WriteLine("请选择行动：1攻击，2防御，3回血");
                int move = int.Parse(Console.ReadLine());
                switch (move)
                {
                    case 1:
                        int dmg = Math.Max(player.atk - boss.def, 1);
                        boss.hp -= dmg;
                        Console.WriteLine($"你发起攻击，造成{dmg}点伤害！");
                        break;
                    case 2:
                        isPlayerDef = true;
                        Console.WriteLine("你选择防御！");
                        break;
                    case 3:
                        player.hp += player.healing;
                        Console.WriteLine($"你回血{player.healing}点！");
                        break;
                }
                if (boss.hp <= 0) break;

                //boss回合
                int bossAct = r.Next(0, 2);
                if (bossAct == 0)
                {
                    int bossDmg = Math.Max(boss.atk - player.def, 1);
                    if (isPlayerDef)
                    {
                        bossDmg /= 2;
                    }
                    player.hp -= bossDmg;
                    Console.WriteLine($"Boss攻击了你，造成{bossDmg}点伤害！");
                }
                else
                {
                    boss.hp += boss.healing;
                    Console.WriteLine($"Boss回血{boss.healing}点！");
                }
            }

            if (player.hp <= 0)
            {
                Console.WriteLine("游戏结束，你输了！");
            }
            else
            {
                Console.WriteLine("恭喜，打败Boss！");
            }
        }
    }
}
