using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Ale.Chronicle;
using Ale.Chronicle.Runtime.UI;
using Ale.Toolkit.Runtime;

namespace Ale.Chronicle.Tests
{
    /// <summary>
    /// 角色信息界面（0.8.0）：预制体按容器等比自适应（UI / 世界空间两种），档案字段按配置排版。
    /// </summary>
    public class CharacterViewTests
    {
        private readonly List<Object> _created = new List<Object>();

        private T Track<T>(T obj) where T : Object { _created.Add(obj); return obj; }

        [TearDown]
        public void Cleanup()
        {
            foreach (var o in _created)
                if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        private RectTransform Container(Vector2 size)
        {
            var go = Track(new GameObject("Container", typeof(RectTransform)));
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            return rt;
        }

        private static Vector3 Center(Vector3[] corners) => (corners[0] + corners[2]) * 0.5f;

        [Test]
        public void FitInto_UiPrefab_ScalesToFitAndCenters()
        {
            var box = Container(new Vector2(336, 336));
            var inst = new GameObject("Portrait", typeof(RectTransform));
            inst.transform.SetParent(box, false);
            var rt = (RectTransform)inst.transform;
            rt.sizeDelta = new Vector2(400, 640);
            rt.pivot = Vector2.zero;   // 偏心的轴心也要居中

            UiwCharacterView.FitInto(inst, box);

            Assert.AreEqual(336f / 640f, rt.localScale.x, 1e-4f, "按较紧的一边等比缩放");
            var c = new Vector3[4]; rt.GetWorldCorners(c);
            var b = new Vector3[4]; box.GetWorldCorners(b);
            Assert.AreEqual(Center(b).x, Center(c).x, 1e-3f);
            Assert.AreEqual(Center(b).y, Center(c).y, 1e-3f);
            Assert.AreEqual(336f, c[2].y - c[0].y, 1e-3f, "高度刚好填满容器");
        }

        [Test]
        public void FitInto_UiPrefab_CentersInOffCenterContainer()
        {
            var box = Container(new Vector2(360, 616));
            box.pivot = new Vector2(0f, 0.5f);   // 容器轴心靠左（贴边排版常见）
            var inst = new GameObject("Portrait", typeof(RectTransform));
            inst.transform.SetParent(box, false);
            var rt = (RectTransform)inst.transform;
            rt.sizeDelta = new Vector2(600, 1500);

            UiwCharacterView.FitInto(inst, box);

            var c = new Vector3[4]; rt.GetWorldCorners(c);
            var b = new Vector3[4]; box.GetWorldCorners(b);
            Assert.AreEqual(Center(b).x, Center(c).x, 1e-3f, "容器轴心不在正中也要放正");
            Assert.AreEqual(Center(b).y, Center(c).y, 1e-3f);
        }

        [Test]
        public void FitInto_WorldSpaceRenderer_FitsBounds()
        {
            var box = Container(new Vector2(336, 336));
            var tex = Track(new Texture2D(100, 200));
            var sprite = Track(Sprite.Create(tex, new Rect(0, 0, 100, 200), new Vector2(0.2f, 0.1f), 100f));   // 1×2 单位，轴心偏下
            var inst = new GameObject("Actor");
            inst.transform.SetParent(box, false);
            var body = new GameObject("Body");
            body.transform.SetParent(inst.transform, false);
            body.AddComponent<SpriteRenderer>().sprite = sprite;

            UiwCharacterView.FitInto(inst, box);

            var bounds = body.GetComponent<SpriteRenderer>().bounds;
            Assert.AreEqual(336f, bounds.size.y, 1e-2f, "高度刚好填满容器");
            Assert.AreEqual(box.position.x, bounds.center.x, 1e-2f);
            Assert.AreEqual(box.position.y, bounds.center.y, 1e-2f);
        }

        [Test]
        public void Profile_FollowsConfiguredFields()
        {
            var db = Track(ScriptableObject.CreateInstance<ChronicleDatabase>());
            var c = new CharacterDefinition("hero");
            db.Characters.Add(c);
            c.values.Add(new AttributeEntry("codename", new AttributeValue(EFieldType.String)));
            c.values.Add(new AttributeEntry("height", new AttributeValue(EFieldType.Float)));
            c.values.Add(new AttributeEntry("likes", new AttributeValue(EFieldType.String, true)));
            c.values.Add(new AttributeEntry("empty", new AttributeValue(EFieldType.String)));
            c.InvalidateEntryCache();
            c.SetAttributeValue<string>("codename", "ZERO");
            c.SetAttributeValue<float>("height", 163f);
            var likes = c.GetAttributeValue("likes"); likes.SetString(0, "清酒"); likes.SetString(1, "黑咖啡");

            var view = Track(new GameObject("View")).AddComponent<UiwCharacterView>();
            typeof(UiwCharacterView).GetField("profileFields", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view,
                new List<UiwCharacterView.ProfileField>
                {
                    new UiwCharacterView.ProfileField("codename", "代号"),
                    new UiwCharacterView.ProfileField("empty", "空字段"),
                    new UiwCharacterView.ProfileField("height", "身高", " cm"),
                    new UiwCharacterView.ProfileField("likes", "喜欢", fullRow: true),
                });
            var build = typeof(UiwCharacterView).GetMethod("BuildProfile", BindingFlags.Instance | BindingFlags.NonPublic);
            string text = (string)build.Invoke(view, new object[] { c, null });

            var lines = text.Split('\n');
            Assert.AreEqual(2, lines.Length, text);
            StringAssert.Contains("代号", lines[0]);
            StringAssert.Contains("<pos=50%>", lines[0], "代号与身高并排");
            StringAssert.Contains("163 cm", lines[0]);
            StringAssert.Contains("清酒、黑咖啡", lines[1], "独占一行，数组用顿号连接");
            StringAssert.DoesNotContain("空字段", text, "空值跳过");
        }

#if ATK_TMP
        [Test]
        public void FitWidth_ShrinksOnlyCellsWiderThanColumn()
        {
            // 只用 ASCII：默认字库（静态图集）里都有，量宽时不会往后备字库加字形、改动工程资源
            var go = Track(new GameObject("Label", typeof(RectTransform)));
            go.SetActive(false);
            var label = go.AddComponent<TMPro.TextMeshProUGUI>();
            label.font = TMPro.TMP_Settings.defaultFontAsset;
            if (!label.font) Assert.Ignore("没有默认 TMP 字库");
            label.fontSize = 20;
            const string shortCell = "<color=#8FA3BF>Age</color> <color=#E6F1FF>23</color>";
            const string longCell = "<color=#8FA3BF>Measurements</color> <color=#E6F1FF>(92, 58, 88)</color>";
            float full = label.GetPreferredValues(longCell, float.PositiveInfinity, float.PositiveInfinity).x;

            Assert.AreEqual(shortCell, UiwCharacterView.FitWidth(label, shortCell, full), "放得下的原样返回");
            Assert.AreEqual(longCell, UiwCharacterView.FitWidth(label, longCell, 0f), "文本框没有尺寸时不处理");

            string fitted = UiwCharacterView.FitWidth(label, longCell, full * 0.5f);
            StringAssert.StartsWith("<size=", fitted);
            float width = label.GetPreferredValues(fitted, float.PositiveInfinity, float.PositiveInfinity).x;
            Assert.LessOrEqual(width, full * 0.5f + 0.5f, "缩小后放得进列宽");
            Assert.Greater(width, full * 0.4f, "按比例缩小，不缩过头");
        }
#endif
    }
}
