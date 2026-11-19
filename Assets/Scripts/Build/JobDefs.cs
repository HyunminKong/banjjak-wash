using System;
using UnityEngine;

namespace Banjjak
{
    public class JobDef
    {
        public string Id, Name, Size, Desc;
        public float Reward;
        public Vector2 Spawn;          // x, z (player faces -Z)
        public Rect Bounds;            // x/z
        public bool FreeLift;
        public Action<CleanJob> Build;
        public string[] DirtKeys;      // shown on the menu card
    }

    public static class JobDefs
    {
        public static readonly JobDef[] All =
        {
            new JobDef { Id = "patio", Name = "뒷마당 테라스", Size = "소형", Reward = 1200, Spawn = new Vector2(0, 6.2f), Bounds = Rect.MinMaxRect(-15, -12, 15, 15),
                Desc = "겨울 내내 방치된 단독주택 마당. 바닥 타일, 벽돌 담장, 벤치와 테이블, 바비큐 그릴까지 맡겨 주셨어요.", Build = Jobs.BuildPatio,
                DirtKeys = new[] { "mud", "leaves", "moss", "mildew", "bird" } },
            new JobDef { Id = "camper", Name = "캠핑카 세차", Size = "소형", Reward = 2000, Spawn = new Vector2(0, 6.8f), Bounds = Rect.MinMaxRect(-15, -12, 15, 15),
                Desc = "장거리 여행에서 막 돌아온 캠핑카와 진입로. 지붕은 옆의 알루미늄 계단을 올라가서 닦으세요.", Build = Jobs2.BuildCamper,
                DirtKeys = new[] { "grime", "bird", "mud", "oil", "leaves" } },
            new JobDef { Id = "park", Name = "동네 놀이터", Size = "중형", Reward = 3200, Spawn = new Vector2(0, 9.6f), Bounds = Rect.MinMaxRect(-16, -14, 16, 16),
                Desc = "미끄럼틀, 그네, 돔, 모래 놀이터 통나무에 이끼와 낙엽, 껌 자국이 잔뜩. 아이들이 오기 전에 끝내 주세요.", Build = Jobs2.BuildPark,
                DirtKeys = new[] { "mud", "leaves", "gum", "moss", "rust" } },
            new JobDef { Id = "house", Name = "2층 주택 외벽", Size = "대형", Reward = 4800, Spawn = new Vector2(-1.8f, 12), Bounds = Rect.MinMaxRect(-18, -12, 18, 18),
                Desc = "곰팡이 핀 외벽, 창문, 현관 데크, 울타리, 기름 범벅 진입로까지. 차고 문 낙서는 거품을 듬뿍 뿌리고 불려서 지우세요.", Build = Jobs2.BuildHouse,
                DirtKeys = new[] { "grime", "mildew", "oil", "tire", "paint" } },
            new JobDef { Id = "skate", Name = "스케이트 공원", Size = "대형", Reward = 6500, Spawn = new Vector2(-5, 10.2f), Bounds = Rect.MinMaxRect(-18, -16, 18, 16),
                Desc = "쿼터파이프, 펀박스, 계단, 레일 곳곳에 찌든 때와 녹, 껌, 낙서. 넓은 바닥이 많은 현장이에요.", Build = Jobs2.BuildSkate,
                DirtKeys = new[] { "grime", "gum", "paint", "rust", "tire" } },
            new JobDef { Id = "plaza", Name = "분수 광장", Size = "초대형", Reward = 9000, Spawn = new Vector2(0, 11), Bounds = Rect.MinMaxRect(-19, -17, 19, 17),
                Desc = "물때 낀 2단 분수대, 새똥 덮인 벤치, 녹슨 가로등, 이끼 낀 화단과 퍼걸러까지 광장 전체를 부탁드려요.", Build = Jobs2.BuildPlaza,
                DirtKeys = new[] { "lime", "algae", "bird", "gum", "rust" } },
            new JobDef { Id = "station", Name = "주유소와 편의점", Size = "초대형", Reward = 12000, Spawn = new Vector2(0, 13.5f), Bounds = Rect.MinMaxRect(-24, -14.5f, 24, 21),
                Desc = "기름과 타이어 자국으로 범벅된 넓은 주유소 바닥, 캐노피, 주유기, 편의점 유리창과 간판, 손님 차까지.", Build = Jobs2.BuildStation,
                DirtKeys = new[] { "oil", "tire", "gum", "soot", "grime" } },
            new JobDef { Id = "tower", Name = "3층 상가 건물", Size = "초대형", Reward = 15000, Spawn = new Vector2(0, 12), Bounds = Rect.MinMaxRect(-22, -19, 22, 16), FreeLift = true,
                Desc = "1층 카페, 2층 사무실, 3층 창고까지 건물 안팎을 통째로. 계단으로 층을 오가며 닦고, 높은 외벽은 현장 작업대(L)로 닦으세요.", Build = Jobs2.BuildTower,
                DirtKeys = new[] { "grime", "mildew", "dust", "gum", "paint" } },
        };

        public static int IndexOf(string id) => Array.FindIndex(All, d => d.Id == id);

        public static CleanJob Create(int index, float mul = 1)
        {
            var d = All[index];
            var job = new CleanJob(d.Id, d.Name, d.Reward)
            {
                Desc = d.Desc,
                Spawn = new Vector3(d.Spawn.x, 0, d.Spawn.y),
                SpawnYaw = 180,
                Bounds = d.Bounds,
                FreeLift = d.FreeLift,
                Mul = mul,
            };
            d.Build(job);
            Jobs.Scenery(job);
            Physics.SyncTransforms();
            job.Weeds = WeedField.Build(job);
            job.Trash = TrashField.Build(job);
            return job;
        }
    }
}
