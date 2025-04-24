using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Text.RegularExpressions;
using System.Linq; // 添加 Linq 命名空间用于合并列表

namespace MusicBridge.Utils
{
    /// <summary>
    /// VirtualKeyboard.xaml 的交互逻辑
    /// </summary>
    public partial class VirtualKeyboard : UserControl
    {
        private TextBox targetTextBox; // 目标文本框 - 键盘输入会影响这个文本框
        
        // 新增：直接搜索模式标志
        public bool DirectSearchMode { get; set; } = false;
        
        // 中文拼音输入相关
        private bool isPinyinMode = true;
        private string pinyinBuffer = string.Empty; // 用于存储拼音字符
        private List<string> candidateWords = new List<string>(); // 候选词列表
        
        // 拼音到汉字的映射字典保持不变
        // 修正：合并了重复键的值并移除了重复项
        private Dictionary<string, List<string>> pinyinToChineseDict = new Dictionary<string, List<string>>
        {
            // 常用的拼音到汉字的映射
            { "a", new List<string> { "啊", "阿", "吖" } },
            { "ai", new List<string> { "爱", "哎", "埃", "艾" } },
            { "an", new List<string> { "安", "按", "案", "暗" } },
            { "ang", new List<string> { "昂", "肮" } },
            { "ao", new List<string> { "奥", "澳", "傲", "熬" } },
            { "ba", new List<string> { "八", "吧", "爸", "把" } },
            { "bai", new List<string> { "白", "百", "败", "拜" } },
            { "ban", new List<string> { "半", "办", "班", "版" } },
            // 合并 "bang"
            { "bang", new List<string> { "帮", "邦", "绑", "棒", "榜", "排行榜", "热门榜" } },
            { "bao", new List<string> { "包", "宝", "保", "抱" } },
            { "bei", new List<string> { "北", "被", "备", "背" } },
            { "ben", new List<string> { "本", "笨", "奔" } },
            { "bi", new List<string> { "比", "必", "笔", "币" } },
            { "bian", new List<string> { "变", "边", "便", "编" } },
            { "biao", new List<string> { "表", "标", "彪" } },
            { "bie", new List<string> { "别", "憋", "瘪" } },
            { "bin", new List<string> { "宾", "彬", "斌", "滨" } },
            { "bo", new List<string> { "波", "博", "播", "伯" } },
            { "bu", new List<string> { "不", "部", "布", "步" } },
            { "ca", new List<string> { "擦", "嚓" } },
            { "cai", new List<string> { "才", "菜", "采", "彩" } },
            { "can", new List<string> { "参", "餐", "残", "惨" } },
             // 合并 "chang"
            { "chang", new List<string> { "长", "常", "场", "厂", "唱", "唱歌", "唱片" } },
            { "chao", new List<string> { "超", "朝", "潮", "吵" } },
            { "che", new List<string> { "车", "撤", "扯" } },
            { "chen", new List<string> { "陈", "沉", "尘", "晨" } },
            { "cheng", new List<string> { "成", "城", "程", "称" } },
            { "chi", new List<string> { "吃", "持", "尺", "迟" } },
            { "chong", new List<string> { "冲", "充", "重", "崇" } },
            { "chou", new List<string> { "抽", "丑", "愁", "筹" } },
            { "chu", new List<string> { "出", "处", "初", "除" } },
            { "ci", new List<string> { "次", "此", "词", "刺" } },
            { "cong", new List<string> { "从", "丛", "聪", "匆" } },
            { "cu", new List<string> { "粗", "促", "醋", "簇" } },
            { "da", new List<string> { "大", "打", "达", "答" } },
            { "dai", new List<string> { "带", "代", "待", "戴" } },
            { "dan", new List<string> { "但", "单", "担", "弹" } },
            { "dang", new List<string> { "当", "党", "挡", "档" } },
            { "dao", new List<string> { "到", "道", "导", "倒" } },
            { "de", new List<string> { "的", "得", "地", "德" } },
            { "deng", new List<string> { "等", "灯", "登", "邓" } },
            { "di", new List<string> { "地", "第", "低", "底" } },
            { "dian", new List<string> { "电", "点", "店", "典" } },
            { "dong", new List<string> { "东", "动", "懂", "洞" } },
            { "dou", new List<string> { "都", "斗", "豆", "抖" } },
            { "du", new List<string> { "读", "度", "独", "毒" } },
            { "duan", new List<string> { "段", "短", "断", "端" } },
            { "duo", new List<string> { "多", "夺", "躲", "朵" } },
            { "e", new List<string> { "恶", "饿", "额", "鹅" } },
            { "er", new List<string> { "而", "二", "儿", "耳" } },
            { "fa", new List<string> { "发", "法", "罚", "乏" } },
            { "fan", new List<string> { "反", "范", "饭", "繁" } },
            { "fang", new List<string> { "方", "放", "房", "访" } },
            { "fei", new List<string> { "非", "飞", "费", "肥" } },
            { "fen", new List<string> { "分", "份", "粉", "愤" } },
            { "feng", new List<string> { "风", "封", "丰", "奉" } },
            { "fu", new List<string> { "父", "复", "付", "负" } },
            { "ga", new List<string> { "嘎", "尬", "尕", "旮" } },
            { "gai", new List<string> { "改", "该", "概", "盖" } },
            { "gan", new List<string> { "干", "感", "赶", "敢" } },
            { "gang", new List<string> { "刚", "港", "钢", "岗" } },
            { "gao", new List<string> { "高", "搞", "告", "稿" } },
            // 合并 "ge"
            { "ge", new List<string> { "歌", "歌曲", "歌手", "歌词" } }, // 注意：这里可能需要根据实际情况调整，原有两个ge，但内容相似
            { "gei", new List<string> { "给" } },
            { "gen", new List<string> { "跟", "根", "亘", "艮" } },
            { "gong", new List<string> { "工", "公", "共", "功" } },
            { "gou", new List<string> { "够", "狗", "购", "沟" } },
            { "gu", new List<string> { "古", "故", "固", "顾" } },
            { "gua", new List<string> { "挂", "瓜", "刮", "寡" } },
            { "guai", new List<string> { "怪", "拐", "乖" } },
            { "guan", new List<string> { "关", "管", "观", "官" } },
            { "guang", new List<string> { "光", "广", "逛" } },
            { "gui", new List<string> { "规", "贵", "鬼", "桂" } },
            { "guo", new List<string> { "国", "过", "果", "郭" } },
            { "ha", new List<string> { "哈", "蛤", "虾", "铪" } },
            { "hai", new List<string> { "还", "海", "害", "孩" } },
            { "han", new List<string> { "汉", "喊", "含", "寒" } },
            { "hang", new List<string> { "行", "航", "杭", "巷" } },
            { "hao", new List<string> { "好", "号", "浩", "毫" } },
            { "he", new List<string> { "和", "合", "河", "何" } },
            { "hei", new List<string> { "黑", "嘿" } },
            { "hen", new List<string> { "很", "狠", "恨" } },
            { "heng", new List<string> { "横", "恒", "衡", "亨" } },
            { "hong", new List<string> { "红", "洪", "宏", "虹" } },
            { "hou", new List<string> { "后", "候", "厚", "吼" } },
            { "hu", new List<string> { "湖", "户", "护", "互" } },
            { "hua", new List<string> { "话", "花", "化", "华" } },
            { "huai", new List<string> { "坏", "怀", "淮", "槐" } },
            { "huan", new List<string> { "还", "环", "患", "换" } },
            { "huang", new List<string> { "黄", "皇", "荒", "慌" } },
            { "hui", new List<string> { "会", "回", "汇", "灰" } },
            { "huo", new List<string> { "或", "活", "火", "获" } },
            { "ji", new List<string> { "几", "机", "己", "期" } },
            { "jia", new List<string> { "家", "加", "价", "假" } },
            { "jian", new List<string> { "见", "间", "建", "件" } },
            { "jiang", new List<string> { "将", "江", "讲", "奖" } },
            { "jiao", new List<string> { "叫", "交", "教", "脚" } },
            { "jie", new List<string> { "接", "街", "节", "解" } },
            { "jin", new List<string> { "进", "金", "近", "仅" } },
            { "jing", new List<string> { "经", "京", "精", "景" } },
            { "jiong", new List<string> { "窘", "炯", "迥", "炅" } },
            { "jiu", new List<string> { "就", "九", "旧", "究" } },
            { "ju", new List<string> { "具", "据", "局", "举" } },
            { "juan", new List<string> { "卷", "捐", "倦", "眷" } },
            { "jue", new List<string> { "觉", "决", "绝", "角" } },
            { "jun", new List<string> { "军", "均", "君", "俊" } },
            { "ka", new List<string> { "卡", "咖", "喀", "咔" } },
            { "kai", new List<string> { "开", "凯", "慨", "楷" } },
            { "kan", new List<string> { "看", "刊", "坎", "砍" } },
            { "kang", new List<string> { "抗", "康", "慷", "炕" } },
            { "kao", new List<string> { "考", "靠", "拷", "烤" } },
            { "ke", new List<string> { "可", "克", "科", "客" } },
            { "ken", new List<string> { "肯", "啃", "垦", "恳" } },
            { "kong", new List<string> { "空", "控", "孔", "恐" } },
            { "kou", new List<string> { "口", "扣", "寇", "叩" } },
            { "ku", new List<string> { "苦", "库", "哭", "酷" } },
            { "kua", new List<string> { "跨", "夸", "垮", "挎" } },
            { "kuai", new List<string> { "快", "块", "筷", "会" } },
            { "kuan", new List<string> { "宽", "款", "髋" } },
            { "kuang", new List<string> { "矿", "狂", "框", "筐" } },
            { "kui", new List<string> { "亏", "愧", "奎", "魁" } },
            { "kun", new List<string> { "困", "昆", "坤", "鲲" } },
            { "kuo", new List<string> { "扩", "阔", "廓" } },
            { "la", new List<string> { "拉", "啦", "蜡", "腊" } },
            { "lai", new List<string> { "来", "赖", "莱", "栽" } },
            { "lan", new List<string> { "蓝", "兰", "烂", "懒" } },
            { "lang", new List<string> { "浪", "郎", "朗", "狼" } },
            { "lao", new List<string> { "老", "劳", "牢", "姥" } },
            { "le", new List<string> { "了", "乐", "勒", "肋" } },
            { "lei", new List<string> { "类", "累", "雷", "泪" } },
            { "leng", new List<string> { "冷", "愣", "棱" } },
            { "li", new List<string> { "里", "力", "利", "立" } },
            { "lian", new List<string> { "连", "联", "练", "脸" } },
            { "liang", new List<string> { "两", "量", "良", "亮" } },
            { "liao", new List<string> { "了", "料", "廖", "辽" } },
            { "lie", new List<string> { "列", "烈", "裂", "猎" } },
            { "lin", new List<string> { "林", "临", "邻", "赁" } },
            { "ling", new List<string> { "零", "领", "另", "令" } },
            { "liu", new List<string> { "六", "流", "留", "刘" } },
            { "long", new List<string> { "龙", "隆", "笼", "拢" } },
            { "lou", new List<string> { "楼", "漏", "露", "陋" } },
            { "lu", new List<string> { "路", "陆", "录", "鹿" } },
            { "luan", new List<string> { "乱", "卵", "峦", "挛" } },
            { "lun", new List<string> { "论", "轮", "伦", "沦" } },
            { "luo", new List<string> { "落", "罗", "锣", "裸" } },
            { "lv", new List<string> { "律", "虑", "率", "绿" } },
            { "ma", new List<string> { "吗", "妈", "马", "码" } },
            { "mai", new List<string> { "买", "卖", "埋", "迈" } },
            { "man", new List<string> { "满", "慢", "漫", "曼" } },
            { "mang", new List<string> { "忙", "盲", "茫", "芒" } },
            { "mao", new List<string> { "毛", "冒", "猫", "贸" } },
            { "mei", new List<string> { "没", "每", "美", "妹" } },
            { "men", new List<string> { "门", "们", "闷", "焖" } },
            { "meng", new List<string> { "梦", "蒙", "盟", "猛" } },
            { "mi", new List<string> { "米", "迷", "密", "秘" } },
            { "mian", new List<string> { "面", "免", "棉", "眠" } },
            { "miao", new List<string> { "秒", "苗", "妙", "庙" } },
            { "min", new List<string> { "民", "敏", "闽", "皿" } },
            { "ming", new List<string> { "名", "明", "命", "鸣" } },
            { "miu", new List<string> { "缪", "谬" } }, // 合并 miu
            { "mo", new List<string> { "莫", "默", "摸", "墨" } },
            { "mu", new List<string> { "木", "母", "墓", "幕" } },
            { "na", new List<string> { "那", "拿", "哪", "纳" } },
            { "nai", new List<string> { "乃", "奶", "耐", "奈" } },
            { "nan", new List<string> { "南", "难", "男", "楠" } },
            { "nang", new List<string> { "囊", "囔", "馕" } },
            { "nao", new List<string> { "脑", "闹", "恼", "孬" } },
            { "ne", new List<string> { "呢", "讷", "哪" } },
            { "nei", new List<string> { "内", "那" } },
            { "nen", new List<string> { "嫩", "恁" } },
            { "neng", new List<string> { "能" } },
            { "ni", new List<string> { "你", "尼", "呢", "泥" } },
            { "nian", new List<string> { "年", "念", "粘", "碾" } },
            { "niang", new List<string> { "娘", "酿", "奖" } },
            { "niao", new List<string> { "鸟", "尿", "袅", "裊" } },
            { "nie", new List<string> { "捏", "聂", "涅", "镊" } },
            { "nin", new List<string> { "您", "恁" } },
            { "ning", new List<string> { "宁", "凝", "拧", "泞" } },
            { "niu", new List<string> { "牛", "纽", "扭", "妞" } },
            { "nong", new List<string> { "农", "浓", "弄", "脓" } },
            { "nu", new List<string> { "女", "努", "怒", "奴" } },
            { "nv", new List<string> { "女", "虐", "疟" } },
            { "nuan", new List<string> { "暖", "暧", "渜" } },
            { "nuo", new List<string> { "诺", "挪", "懦", "糯" } },
            { "o", new List<string> { "哦", "噢", "喔" } },
            { "ou", new List<string> { "欧", "偶", "呕", "藕" } },
            // 合并 "pai"
            { "pa", new List<string> { "怕", "爬", "帕", "啪" } },
            { "pai", new List<string> { "派", "排", "牌", "拍", "排行", "排行榜" } },
            { "pan", new List<string> { "盘", "判", "盼", "叛" } },
            { "pang", new List<string> { "旁", "胖", "庞", "乓" } },
            { "pao", new List<string> { "跑", "抛", "炮", "泡" } },
            { "pei", new List<string> { "配", "培", "佩", "赔" } },
            { "pen", new List<string> { "喷", "盆", "湓" } },
            { "peng", new List<string> { "朋", "鹏", "碰", "彭" } },
            { "pi", new List<string> { "皮", "批", "披", "疲" } },
            { "pian", new List<string> { "片", "骗", "篇", "偏" } },
            { "piao", new List<string> { "票", "飘", "漂", "瓢" } },
            { "pie", new List<string> { "撇", "瞥", "丿", "苤" } },
            { "pin", new List<string> { "品", "贫", "聘", "拼" } },
            { "ping", new List<string> { "平", "评", "凭", "瓶" } },
            { "po", new List<string> { "破", "婆", "迫", "泼" } },
            { "pou", new List<string> { "剖", "裒", "掊" } },
            { "pu", new List<string> { "普", "铺", "扑", "朴" } },
            { "qi", new List<string> { "起", "其", "期", "气" } },
            { "qia", new List<string> { "恰", "洽", "掐", "卡" } },
            { "qian", new List<string> { "前", "钱", "千", "欠" } },
            { "qiang", new List<string> { "强", "墙", "抢", "枪" } },
            { "qiao", new List<string> { "桥", "巧", "悄", "敲" } },
            { "qie", new List<string> { "切", "且", "窃", "茄" } },
            { "qin", new List<string> { "亲", "侵", "勤", "琴" } },
            { "qing", new List<string> { "请", "清", "青", "轻" } },
            { "qiong", new List<string> { "穷", "琼", "穹", "茕" } },
            { "qiu", new List<string> { "求", "秋", "球", "丘" } },
            // 合并 "qu"
            { "qu", new List<string> { "去", "取", "区", "曲", "曲目", "曲调", "歌曲" } },
            { "quan", new List<string> { "全", "权", "圈", "拳" } },
            { "que", new List<string> { "却", "确", "缺", "雀" } },
            { "qun", new List<string> { "群", "裙" } },
            { "ran", new List<string> { "然", "染", "燃" } },
            { "rang", new List<string> { "让", "嚷", "攘", "壤" } },
            { "rao", new List<string> { "饶", "扰", "绕" } },
            { "re", new List<string> { "热", "惹", "喏" } },
            { "ren", new List<string> { "人", "认", "任", "忍" } },
            { "reng", new List<string> { "仍", "扔", "壬" } },
            { "ri", new List<string> { "日", "入", "壬" } },
            { "rong", new List<string> { "容", "荣", "融", "溶" } },
            { "rou", new List<string> { "肉", "揉", "柔", "蹂" } },
            { "ru", new List<string> { "如", "入", "辱", "儒" } },
            { "ruan", new List<string> { "软", "阮", "朊" } },
            { "rui", new List<string> { "瑞", "锐", "睿", "芮" } },
            { "run", new List<string> { "润", "闰" } },
            { "ruo", new List<string> { "若", "弱", "偌" } },
            { "sa", new List<string> { "撒", "洒", "萨", "卅" } },
            { "sai", new List<string> { "赛", "塞", "鳃", "腮" } },
            { "san", new List<string> { "三", "散", "伞", "叁" } },
            { "sang", new List<string> { "桑", "丧", "嗓", "搡" } },
            { "sao", new List<string> { "扫", "嫂", "搔", "臊" } },
            { "se", new List<string> { "色", "涩", "瑟", "塞" } },
            { "sen", new List<string> { "森", "僧" } },
            { "sha", new List<string> { "杀", "沙", "傻", "啥" } },
            { "shai", new List<string> { "晒", "筛", "色" } },
            { "shan", new List<string> { "山", "删", "闪", "衫" } },
            { "shang", new List<string> { "上", "商", "尚", "伤" } },
            { "shao", new List<string> { "少", "烧", "绍", "稍" } },
            { "she", new List<string> { "设", "社", "射", "蛇" } },
            { "shen", new List<string> { "深", "身", "神", "沈" } },
            { "sheng", new List<string> { "生", "声", "升", "省" } },
            { "shi", new List<string> { "是", "时", "事", "实" } },
            { "shou", new List<string> { "手", "首", "收", "守" } },
            { "shu", new List<string> { "数", "书", "树", "属" } },
            { "shua", new List<string> { "刷", "耍" } },
            { "shuai", new List<string> { "帅", "摔", "衰", "甩" } },
            { "shuan", new List<string> { "栓", "拴", "涮" } },
            { "shuang", new List<string> { "双", "霜", "爽" } },
            { "shui", new List<string> { "水", "睡", "税", "谁" } },
            { "shun", new List<string> { "顺", "瞬", "舜" } },
            { "shuo", new List<string> { "说", "硕", "烁", "朔" } },
            { "si", new List<string> { "四", "死", "思", "私" } },
            { "song", new List<string> { "送", "松", "宋", "诵" } },
            { "sou", new List<string> { "搜", "艘", "嗽", "擞" } },
            { "su", new List<string> { "素", "速", "苏", "诉" } },
            { "suan", new List<string> { "算", "酸", "蒜" } },
            { "sui", new List<string> { "虽", "随", "岁", "碎" } },
            { "sun", new List<string> { "孙", "损", "笋", "隼" } },
            { "suo", new List<string> { "所", "索", "缩", "锁" } },
            { "ta", new List<string> { "他", "她", "它", "塔" } },
            { "tai", new List<string> { "太", "台", "抬", "泰" } },
            { "tan", new List<string> { "谈", "坦", "探", "叹" } },
            { "tang", new List<string> { "糖", "唐", "堂", "汤" } },
            { "tao", new List<string> { "套", "讨", "逃", "桃" } },
            { "te", new List<string> { "特", "忑", "忒" } },
            { "teng", new List<string> { "疼", "腾", "藤" } },
            { "ti", new List<string> { "提", "题", "体", "替" } },
            { "tian", new List<string> { "天", "田", "填", "甜" } },
            { "tiao", new List<string> { "条", "跳", "挑", "调" } },
            { "tie", new List<string> { "铁", "贴", "帖" } },
            // 合并 "ting"
            { "ting", new List<string> { "听", "停", "厅", "亭", "听歌", "聆听" } },
            { "tong", new List<string> { "通", "同", "统", "痛" } },
            { "tou", new List<string> { "头", "投", "透", "偷" } },
            { "tu", new List<string> { "图", "土", "突", "途" } },
            { "tuan", new List<string> { "团", "湍", "抟", "彖" } },
            { "tui", new List<string> { "推", "退", "腿" } },
            { "tun", new List<string> { "吞", "屯", "臀", "囤" } },
            { "tuo", new List<string> { "托", "拖", "脱", "驼" } },
            { "wa", new List<string> { "挖", "瓦", "蛙", "娃" } },
            { "wai", new List<string> { "外", "歪", "崴" } },
            { "wan", new List<string> { "完", "万", "晚", "碗" } },
            { "wang", new List<string> { "网", "忘", "亡", "王" } },
            { "wei", new List<string> { "为", "未", "位", "味" } },
            { "wen", new List<string> { "问", "文", "温", "稳" } },
            { "weng", new List<string> { "翁", "瓮", "嗡" } },
            { "wo", new List<string> { "我", "握", "窝", "卧" } },
            { "wu", new List<string> { "无", "五", "物", "务" } },
            { "xi", new List<string> { "西", "系", "席", "喜" } },
            { "xia", new List<string> { "下", "夏", "吓", "虾" } },
            { "xian", new List<string> { "先", "线", "现", "显" } },
            { "xiang", new List<string> { "想", "相", "向", "项" } },
            { "xiao", new List<string> { "小", "笑", "校", "效" } },
            { "xie", new List<string> { "些", "写", "谢", "血" } },
            // 合并 "xin"
            { "xin", new List<string> { "新", "心", "信", "欣", "新歌", "新专辑" } },
            { "xing", new List<string> { "行", "性", "形", "兴" } },
            { "xiong", new List<string> { "雄", "熊", "胸", "凶" } },
            { "xiu", new List<string> { "修", "休", "秀", "绣" } },
            { "xu", new List<string> { "许", "须", "徐", "序" } },
            { "xuan", new List<string> { "选", "宣", "轩", "旋" } },
            { "xue", new List<string> { "学", "雪", "血", "穴" } },
            { "xun", new List<string> { "寻", "训", "迅", "讯" } },
            { "ya", new List<string> { "亚", "压", "呀", "牙" } },
            { "yan", new List<string> { "眼", "言", "严", "演" } },
            { "yang", new List<string> { "样", "洋", "阳", "养" } },
            { "yao", new List<string> { "要", "药", "摇", "腰" } },
            { "ye", new List<string> { "也", "业", "夜", "叶" } },
            { "yi", new List<string> { "一", "以", "义", "意" } },
            { "yin", new List<string> { "因", "音", "银", "引" } },
            { "ying", new List<string> { "应", "英", "影", "营" } },
            { "yo", new List<string> { "哟", "唷" } },
            { "yong", new List<string> { "用", "永", "勇", "涌" } },
            { "you", new List<string> { "有", "又", "由", "友" } },
            { "yu", new List<string> { "于", "与", "语", "育" } },
            { "yuan", new List<string> { "元", "原", "员", "远" } },
            // 合并 "yue"
            { "yue", new List<string> { "乐", "乐队", "音乐", "悦", "月", "越", "约", "阅" } },
            { "yun", new List<string> { "云", "运", "晕", "韵" } },
            { "za", new List<string> { "杂", "咋", "砸", "咂" } },
            { "zai", new List<string> { "在", "再", "载", "灾" } },
            { "zan", new List<string> { "赞", "暂", "咱", "攒" } },
            { "zang", new List<string> { "脏", "葬", "藏", "臧" } },
            { "zao", new List<string> { "早", "造", "糟", "藻" } },
            { "ze", new List<string> { "则", "择", "泽", "责" } },
            { "zei", new List<string> { "贼" } },
            { "zen", new List<string> { "怎", "谮", "甑" } },
            { "zeng", new List<string> { "增", "赠", "憎", "曾" } },
            { "zha", new List<string> { "查", "炸", "渣", "闸" } },
            { "zhai", new List<string> { "摘", "窄", "宅", "债" } },
            { "zhan", new List<string> { "站", "占", "战", "展" } },
            { "zhang", new List<string> { "长", "张", "章", "掌" } },
            { "zhao", new List<string> { "照", "找", "招", "赵" } },
            { "zhe", new List<string> { "这", "者", "折", "哲" } },
            { "zhen", new List<string> { "真", "镇", "针", "枕" } },
            { "zheng", new List<string> { "正", "整", "证", "政" } },
            { "zhi", new List<string> { "之", "只", "知", "指" } },
            { "zhong", new List<string> { "中", "重", "种", "众" } },
            { "zhou", new List<string> { "周", "州", "洲", "粥" } },
            { "zhu", new List<string> { "主", "注", "住", "助" } },
            { "zhua", new List<string> { "抓", "爪", "挝" } },
            { "zhuai", new List<string> { "拽" } },
            { "zhuan", new List<string> { "专", "转", "传", "赚" } },
            { "zhuang", new List<string> { "装", "状", "壮", "撞" } },
            { "zhui", new List<string> { "追", "坠", "缀", "椎" } },
            { "zhun", new List<string> { "准", "谆", "屯", "肫" } },
            { "zhuo", new List<string> { "着", "桌", "卓", "捉" } },
            { "zi", new List<string> { "子", "自", "字", "资" } },
            { "zong", new List<string> { "总", "宗", "纵", "综" } },
            { "zou", new List<string> { "走", "奏", "揍", "邹" } },
            { "zu", new List<string> { "组", "足", "族", "祖" } },
            { "zuan", new List<string> { "钻", "纂", "赚", "缵" } },
            { "zui", new List<string> { "最", "嘴", "醉", "罪" } },
            { "zun", new List<string> { "尊", "遵", "樽", "鳟" } },
            { "zuo", new List<string> { "作", "做", "坐", "左" } },
            
            // 音乐相关 - 移除重复的 ge, chang, yue, qu, ting
            { "music", new List<string> { "音乐", "音乐会", "音乐剧" } },
            { "yinyue", new List<string> { "音乐", "音乐会", "音乐剧" } },
            { "gequ", new List<string> { "歌曲", "歌曲集", "歌曲本" } },
            { "geshou", new List<string> { "歌手", "歌唱家", "演唱者" } },
            { "mingxing", new List<string> { "明星", "偶像", "艺人" } },
            { "mingzu", new List<string> { "民族", "民族音乐" } },
            { "liuxing", new List<string> { "流行", "流行歌曲", "流行音乐" } },
            { "gudian", new List<string> { "古典", "古典音乐" } },
            { "yaogun", new List<string> { "摇滚", "摇滚乐", "摇滚音乐" } },
            { "sousuo", new List<string> { "搜索", "查找", "搜寻" } },
            
            // 常用搜索词 - 移除重复的 ting, pai, bang, xin
            { "search", new List<string> { "搜索", "查找" } },
            { "find", new List<string> { "查找", "寻找" } },
            { "listen", new List<string> { "听", "聆听" } },
            { "paihang", new List<string> { "排行", "排行榜" } },
            { "zuixin", new List<string> { "最新", "新歌", "新专辑" } },
            
            // 添加更多音乐相关词汇
            { "zhuanji", new List<string> { "专辑", "专辑集" } },
            { "mv", new List<string> { "MV", "音乐录影带", "音乐视频" } },
            { "zhubo", new List<string> { "主播", "播音员", "主持人" } },
            { "diantai", new List<string> { "电台", "广播电台", "网络电台" } },
            { "pingfen", new List<string> { "评分", "打分", "评价" } },
            { "pinglun", new List<string> { "评论", "留言", "点评" } },
            { "shoucangjia", new List<string> { "收藏夹", "我的收藏" } },
            { "shoucang", new List<string> { "收藏", "收藏歌曲", "收藏专辑" } },
            { "xiazai", new List<string> { "下载", "下载歌曲", "下载专辑" } },
            { "bofang", new List<string> { "播放", "播放歌曲", "播放专辑" } }
        };

        // 搜索完成后的回调事件
        public event EventHandler<string> SearchCompleted;
        
        public VirtualKeyboard()
        {
            InitializeComponent();
        }
        
        // 初始化键盘并设置目标文本框
        public void Initialize(TextBox targetTextBox)
        {
            this.targetTextBox = targetTextBox;
            
            // 清空输入框内容
            InputTextBox.Text = string.Empty;
            
            // 如果是直接搜索模式，显示提示文本
            if (DirectSearchMode)
            {
                InputTextBox.Text = "";
                TitleTextBlock.Text = "直接搜索音乐";
                SearchButton.Content = "搜索";
            }
            else if (targetTextBox != null)
            {
                // 非直接模式，将目标文本框的内容复制到虚拟键盘的输入框
                InputTextBox.Text = targetTextBox.Text;
                TitleTextBlock.Text = "搜索";
                SearchButton.Content = "搜索";
            }
            
            UpdateCandidates(); // 更新候选词
        }
        
        // 处理键盘按键点击事件
        private void KeyButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button != null)
            {
                string key = button.Content.ToString();
                
                // 处理空格键
                if (key == "空格" || key == " ")
                {
                    key = " ";
                }
                
                // 添加字符到输入框
                InputTextBox.Text += key;
                InputTextBox.CaretIndex = InputTextBox.Text.Length; // 将光标移动到末尾
                
                // 如果是拼音模式，则更新候选词
                if (isPinyinMode && KeyboardTabControl.SelectedIndex == 0)
                {
                    pinyinBuffer += key;
                    UpdateCandidates();
                }
            }
        }
        
        // 处理退格键点击事件
        private void BackspaceButton_Click(object sender, RoutedEventArgs e)
        {
            if (InputTextBox.Text.Length > 0)
            {
                InputTextBox.Text = InputTextBox.Text.Substring(0, InputTextBox.Text.Length - 1);
                InputTextBox.CaretIndex = InputTextBox.Text.Length; // 将光标移动到末尾
                
                // 如果是拼音模式，则更新拼音缓冲区和候选词
                if (isPinyinMode && KeyboardTabControl.SelectedIndex == 0 && pinyinBuffer.Length > 0)
                {
                    pinyinBuffer = pinyinBuffer.Substring(0, pinyinBuffer.Length - 1);
                    UpdateCandidates();
                }
            }
        }
        
        // 处理清除按钮点击事件
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            InputTextBox.Text = string.Empty;
            pinyinBuffer = string.Empty;
            CandidatesPanel.Children.Clear();
        }
        
        // 处理取消按钮点击事件
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // 触发 SearchCompleted 事件但不传递搜索词
            SearchCompleted?.Invoke(this, string.Empty);
        }
        
        // 处理搜索按钮点击事件
        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(InputTextBox.Text))
            {
                if (!DirectSearchMode && targetTextBox != null)
                {
                    // 非直接模式：将输入框的内容复制到目标文本框
                    targetTextBox.Text = InputTextBox.Text;
                }
                
                // 触发 SearchCompleted 事件并传递搜索词
                SearchCompleted?.Invoke(this, InputTextBox.Text);
            }
        }
        
        // 处理候选词点击事件
        private void CandidateButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button != null)
            {
                string candidateWord = button.Content.ToString();
                
                // 替换拼音缓冲区对应的文本
                if (InputTextBox.Text.Length >= pinyinBuffer.Length)
                {
                    string prefix = InputTextBox.Text.Substring(0, InputTextBox.Text.Length - pinyinBuffer.Length);
                    InputTextBox.Text = prefix + candidateWord;
                    InputTextBox.CaretIndex = InputTextBox.Text.Length;
                }
                
                // 清空拼音缓冲区和候选词面板
                pinyinBuffer = string.Empty;
                CandidatesPanel.Children.Clear();
            }
        }
        
        // 更新候选词面板
        private void UpdateCandidates()
        {
            CandidatesPanel.Children.Clear();
            
            if (string.IsNullOrEmpty(pinyinBuffer))
                return;
            
            // 尝试获取拼音对应的汉字列表
            candidateWords.Clear();
            List<string> matchedWords = FindMatchedCandidates(pinyinBuffer);
            
            // 显示候选词按钮
            foreach (string word in matchedWords)
            {
                Button candidateButton = new Button
                {
                    Content = word,
                    Style = (Style)FindResource("CandidateButtonStyle"),
                    Margin = new Thickness(2)
                };
                
                candidateButton.Click += CandidateButton_Click;
                CandidatesPanel.Children.Add(candidateButton);
            }
        }
        
        // 根据拼音缓冲区找到匹配的候选词
        private List<string> FindMatchedCandidates(string pinyin)
        {
            List<string> result = new List<string>();
            string lowerPinyin = pinyin.ToLower(); // 转换为小写以进行不区分大小写的比较
            
            // 查找完全匹配的拼音
            if (pinyinToChineseDict.TryGetValue(lowerPinyin, out var exactMatchWords)) // 使用 TryGetValue 提高效率
            {
                result.AddRange(exactMatchWords);
            }
            
            // 查找前缀匹配的拼音
            foreach (var entry in pinyinToChineseDict)
            {
                // 确保键不为空且以输入拼音开头，并且不是完全匹配（已处理）
                if (!string.IsNullOrEmpty(entry.Key) && entry.Key.StartsWith(lowerPinyin) && entry.Key != lowerPinyin)
                {
                    // 添加拼音作为候选词 (去重)
                    if (!result.Contains(entry.Key))
                    {
                        result.Add(entry.Key);
                    }
                    // 也可以考虑添加这些拼音对应的汉字作为候选，根据需要决定
                    // result.AddRange(entry.Value.Where(word => !result.Contains(word)));
                }
            }
            
            // 如果没有匹配项，并且输入的是合法的拼音格式（例如，全是字母），则可以考虑返回拼音本身
            // 这里简化处理：如果没有找到任何匹配（包括完全匹配和前缀匹配），则返回原始输入
            if (result.Count == 0 && !string.IsNullOrEmpty(pinyin))
            {
                 // 检查是否只包含字母，以避免将非拼音字符也加入
                if (Regex.IsMatch(pinyin, @"^[a-zA-Z]+$"))
                {
                    result.Add(pinyin);
                }
            }
            
            // 对结果进行去重，以防万一有重复添加
            return result.Distinct().ToList(); // 使用 Linq 去重
        }
        
        // 设置拼音模式或英文模式
        public void SetPinyinMode(bool isPinyin)
        {
            isPinyinMode = isPinyin;
            KeyboardTabControl.SelectedIndex = isPinyin ? 0 : 1;
        }
    }
}