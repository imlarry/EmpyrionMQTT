using System.Collections.Generic;

namespace EDNAClient.Core
{
    // High-level role of a block, independent of which skill is using it.
    // BlockClassifier is the single source of truth; skills (FloorMap, Tomography,
    // future skills) consult it so block-identity decisions are centralized.
    public enum BlockCategory
    {
        Unknown,           // default: treat as solid hull / wall

        // ── Sightline / scan-volume exceptions ────────────────────────────────
        Window,            // glass, hardened glass, cockpit canopy -- see-through
        EmptySpace,        // intentionally hollow / decorative open volumes

        // ── Traversal ─────────────────────────────────────────────────────────
        Door,              // doors, hangar doors, shutter doors, blast doors, trap doors
        Walkway,
        Ramp,              // standalone ramps + boarding ramps
        Stair,
        Railing,

        // ── Interaction surfaces ──────────────────────────────────────────────
        Console,           // trade console, control station, computer table
        LcdScreen,         // LCD screens, holo screens, projectors
        Sensor,            // motion sensors, light barriers, cameras, detectors
        Spawner,           // entity / player / drone spawners

        // ── Power & energy ────────────────────────────────────────────────────
        PowerGenerator,    // generators, solar panels, solar generator
        Capacitor,

        // ── Resource storage ──────────────────────────────────────────────────
        FuelTank,          // fuel + pentaxid (warp fuel)
        OxygenTank,
        Container,         // cargo containers, ammo boxes, harvest containers, closets

        // ── Functional gear (occupy hull cells; candidates for marker rendering) ─
        Weapon,            // fixed weapons, turrets, sentry guns
        Thruster,          // jet thrusters, RCS, hover engines
        LandingGear,
        Cockpit,           // cockpits + passenger seats
        Antenna,
        Light,             // lights, lanterns, spotlights
        Constructor,       // fabricators, food processors, furnaces, deconstructors
        Utility,           // repair bays, trading station, ATM, clone chambers, life-support generators
        ShieldGenerator,
        Core,
        WarpDrive,
        CpuExtender,
        Wing,              // SV / CV aerodynamic wings
        Truss,             // open-frame structural

        // ── Population ────────────────────────────────────────────────────────
        Npc,               // standing NPCs, dancing NPCs

        // ── Misc ──────────────────────────────────────────────────────────────
        Decoration,        // furniture, beds, posters, scenic props
    }

    public static class BlockClassifier
    {
        // ── Lookups ───────────────────────────────────────────────────────────

        private static readonly Dictionary<int, BlockCategory> Map;

        static BlockClassifier()
        {
            Map = new Dictionary<int, BlockCategory>();
            Add(Windows,          BlockCategory.Window);
            Add(EmptySpaces,      BlockCategory.EmptySpace);
            Add(Doors,            BlockCategory.Door);
            Add(Walkways,         BlockCategory.Walkway);
            Add(Ramps,            BlockCategory.Ramp);
            Add(Stairs,           BlockCategory.Stair);
            Add(Railings,         BlockCategory.Railing);
            Add(Consoles,         BlockCategory.Console);
            Add(LcdScreens,       BlockCategory.LcdScreen);
            Add(Sensors,          BlockCategory.Sensor);
            Add(Spawners,         BlockCategory.Spawner);
            Add(PowerGenerators,  BlockCategory.PowerGenerator);
            Add(Capacitors,       BlockCategory.Capacitor);
            Add(FuelTanks,        BlockCategory.FuelTank);
            Add(OxygenTanks,      BlockCategory.OxygenTank);
            Add(Containers,       BlockCategory.Container);
            Add(Weapons,          BlockCategory.Weapon);
            Add(Thrusters,        BlockCategory.Thruster);
            Add(LandingGears,     BlockCategory.LandingGear);
            Add(Cockpits,         BlockCategory.Cockpit);
            Add(Antennas,         BlockCategory.Antenna);
            Add(Lights,           BlockCategory.Light);
            Add(Constructors,     BlockCategory.Constructor);
            Add(Utilities,        BlockCategory.Utility);
            Add(ShieldGenerators, BlockCategory.ShieldGenerator);
            Add(Cores,            BlockCategory.Core);
            Add(WarpDrives,       BlockCategory.WarpDrive);
            Add(CpuExtenders,     BlockCategory.CpuExtender);
            Add(Wings,            BlockCategory.Wing);
            Add(Trusses,          BlockCategory.Truss);
            Add(Npcs,             BlockCategory.Npc);
            Add(Decorations,      BlockCategory.Decoration);
        }

        private static void Add(int[] ids, BlockCategory cat)
        {
            foreach (var id in ids) Map[id] = cat;
        }

        public static BlockCategory Classify(int typeId) =>
            Map.TryGetValue(typeId, out var c) ? c : BlockCategory.Unknown;

        public static IEnumerable<int> TypeIdsFor(BlockCategory category)
        {
            foreach (var kv in Map)
                if (kv.Value == category) yield return kv.Key;
        }

        // ── Category populations ──────────────────────────────────────────────
        // IDs derived from BlockAndItemMapping. Add new IDs to the matching array;
        // the static constructor rebuilds the map.

        private static readonly int[] Windows = {
            // Vert / Sloped windows (early IDs)
            275, 276, 277, 285, 286,
            // Window_v* / Window_s* / Window_sd* / Window_c* / Window_cr* family
            770, 771, 795, 796, 797, 798, 799, 800, 801, 802,
            803, 804, 805, 806, 807, 808, 809, 810, 811, 812,
            813, 814, 815, 816, 817, 818,
            // Thick / ThickInv variants
            966, 967, 968, 977, 978, 979, 980, 981, 982, 983,
            984, 985, 986, 989, 990, 991, 992, 993, 994, 995,
            996, 997, 998, 999, 1000, 1001,
            // Translucent armored shutters (treated as see-through)
            972, 973,
            // 3-side / L / cr-extended windows
            1183, 1184, 1185, 1186, 1187, 1188, 1189, 1190,
            1197, 1198, 1199, 1200, 1201, 1202, 1203, 1204, 1205, 1206,
            1207, 1208, 1209, 1210, 1211, 1212, 1213, 1214, 1215, 1216,
            1217, 1218, 1219, 1220,
            // Heavy windows
            1550, 1551, 1552, 1553, 1554, 1555, 1556, 1557, 1558, 1559,
            1560, 1561, 1629, 1630, 1631, 1632, 1633, 1634, 1635, 1636,
            1906, 1907, 1908, 1909, 1910, 1911, 1912, 1913, 1914, 1915,
            1916, 1917, 1918, 1919, 1920, 1921, 1922, 1923, 1924, 1925,
            1926, 1927, 1928, 1929, 1930, 1931, 1932, 1933, 1934, 1935,
            1936, 1937, 1938, 1939, 1940, 1941, 1942, 1943, 1944, 1945,
            1946, 1947, 1948, 1949, 1959, 1983, 1984, 1985, 1986, 1987,
            1988, 1989, 1990, 1991, 1992, 1993, 1994, 1995, 1996, 1997,
            1998, 1999, 2000, 2001, 2002, 2003, 2004, 2005, 2006, 2007,
            2008, 2022,
            // Heavy window detailed
            2094, 2095, 2096, 2097, 2098, 2099, 2100, 2101, 2102, 2103,
            2104, 2105, 2106, 2107, 2108, 2109, 2110, 2111, 2112, 2113,
            2114, 2115, 2116, 2117, 2118, 2119, 2120, 2121, 2122, 2123,
            2124, 2125,
            // Parent group IDs (defensive; usually do not appear in row data)
            545, 836, 974, 976, 1128, 1129, 1549, 2094,
        };

        // Blocks that represent intentionally empty / hollow volume.
        private static readonly int[] EmptySpaces = {
            338,  // OutsideBlock
            530,  // DummyBlock
            1681, // FillerBlock
        };

        private static readonly int[] Doors = {
            // Standard doors
            281, 460, 965, 1003, 1004, 1113, 1114, 1115,
            1233, 1234, 1235, 1429, 1430, 1376,
            // Door blocks parents (groups)
            1002, 1112, 1375, 1008, 1016, 1020, 1136, 1112,
            // Door-corner / round / centered variants
            1815, 1816, 1817, 1885, 1886, 1887,
            1953, 1954, 1955,
            2009, 2010, 2011, 2012, 2013, 2014,
            2015, 2016, 2017, 2018, 2019, 2020,
            // Hangar doors
            975, 987, 988, 1005, 1006, 1007, 1089, 1090,
            1889, 1890, 1891, 1892, 1893,
            // Shutter doors
            1011, 1012, 1013, 1014, 1015, 1017, 1018, 1019, 1021,
            1036, 1037, 1894, 1895, 1896, 1897, 1898,
            // Blast shutter doors
            1137, 1138, 2061, 2062, 2063, 2064, 2065, 2066, 2067, 2068,
            2069, 2070, 2071,
            // Trap doors
            1258, 1264,
        };

        private static readonly int[] Walkways = {
            674, 675, 676, 884, 885,
            // Parents
            838, 1690, 1691,
        };

        private static readonly int[] Ramps = {
            443, 444, 446, 820, 821, 819,
            1022, 1023, 1024, 1025, 1026, 1027, 1028, 1029, 1030,
            1031, 1692, 1693,
            1707, 1708, 1709, 1710, 1706,
            1818, 1819, 1820, 1821, 1822, 1823,
            1899, 1900, 1901, 1902, 1903, 1904, 1905,
        };

        private static readonly int[] Stairs = {
            461, 672, 673, 839, 1125, 1126,
            1440, 1441, 1442, 1443, 1444, 1445,
        };

        private static readonly int[] Railings = {
            333, 334, 681, 682, 691, 692,
            1191, 1192, 1193, 1194, 1195, 1196,
            1221, 1222, 1223, 1224, 1225, 1226,
            1967, 1968, 1969, 1970, 1971, 1972, 1973, 1974, 1975,
            1976, 1977, 1978, 1979, 1980, 1981, 1982, 2021,
        };

        private static readonly int[] Consoles = {
            258, 261, 344, 553, 618, 635, 636, 637, 638, 727,
            1087, 1243, 1247, 1252, 1299, 1455, 1457,
            // Parents
            928,
        };

        private static readonly int[] LcdScreens = {
            950, 951, 952, 953, 954,
            1095, 1096, 1097, 1098, 1099, 1100, 1101, 1102, 1103,
            1400,
        };

        private static readonly int[] Sensors = {
            421, 699, 1228, 1229, 1259, 1260, 1265,
            1304, 1488, 1575, 1576, 1642,
            // Parents
            1230, 1257,
        };

        private static readonly int[] Spawners = {
            // Entity spawners
            658, 659, 660, 661, 662, 663, 664, 665, 666, 667, 668,
            // Player spawners
            1374, 1378, 1379, 1385, 1449,
            // Drone bays / spawners
            1048, 1049, 1586, 1587,
            // Respawn
            2076, 2077,
        };

        private static readonly int[] PowerGenerators = {
            418, 469, 471, 498, 1034,
            // Solar
            447, 448, 1495, 1496, 1497, 1498, 1499,
            1510, 1511, 1512, 1514, 1515, 1516,
            // Parents
            1494, 1513,
        };

        private static readonly int[] Capacitors = {
            256, 426,
        };

        private static readonly int[] FuelTanks = {
            259, 260, 419, 425, 470, 1035,
            // Pentaxid (warp fuel)
            336, 1437,
        };

        private static readonly int[] OxygenTanks = {
            263, 422, 499, 717,
        };

        private static readonly int[] Containers = {
            273, 274, 331, 514, 541, 542, 543, 544,
            686, 724, 728, 732,
            1081, 1082, 1083, 1084, 1232,
            1676, 1677, 1678,
            1682, 1683, 1684, 1685, 1686, 1687, 1688, 1689,
            1713, 1714, 1715, 1716,
            1769, 1770, 1771, 1772,
            1795, 1796, 1797, 1814,
            // Parents
            535, 1008, 1711, 1712,
        };

        private static readonly int[] Weapons = {
            // Fixed weapons
            428, 429, 430, 431, 432, 489, 646, 647,
            // Turrets - MS / SV / GV / Base / Alien / Enemy
            283, 284, 287, 288, 289, 320, 321, 322, 323, 324,
            325, 326, 327, 345, 491, 492, 550, 551, 552, 555,
            557, 648, 649, 650, 684, 685, 700, 701, 702, 716,
            769, 1104, 1105, 1142, 1143, 1144, 1145, 1146, 1147,
            1148, 1149, 1517, 1518, 1652, 1653, 1654, 1655, 1656,
            1657, 1658, 1664, 1665, 1666, 1667, 1668, 1669,
            1679, 1680, 1773, 2126, 2127, 2128, 2129,
            // Sentry guns
            565, 566, 567, 568, 1670, 1671, 1672, 1674, 1675, 1717, 1718,
            // Spring gun
            1261,
            // Parents
            282, 290, 1637, 1638, 1639, 1640, 1641,
            1648, 1649, 1650, 1651, 1659, 1660, 1661, 1662, 1663, 1673,
        };

        private static readonly int[] Thrusters = {
            // Thrusters
            449, 450, 451, 452, 453, 454, 455, 456, 457, 458,
            497, 537, 538, 539, 540, 546, 547, 548, 589, 590,
            694, 695, 696, 697, 698, 768, 1106, 1417, 1585,
            1591, 1592, 1774, 1775, 1776,
            // Hover engines
            603, 1127, 1130, 1484,
            // RCS
            272, 420, 561, 604, 782, 934,
            // Parents
            536, 772, 778, 835, 1107,
        };

        private static readonly int[] LandingGears = {
            417, 445, 722, 723, 729, 730, 779, 780, 1064,
            1116, 1117, 1121, 1122, 1123, 1124,
            1694, 1695, 1696, 1697, 1698, 1699, 1700, 1701, 1702,
            1703, 1704, 1705,
            // Parents
            1118, 1119, 1120,
        };

        private static readonly int[] Cockpits = {
            // Cockpits
            257, 267, 427, 433, 434, 459, 632, 633, 670, 671,
            688, 689, 690, 963, 1009, 1091, 1092, 1094, 1237,
            1487, 1800, 1801, 1802, 1803, 1804, 1805, 1806, 1807,
            1950, 1951,
            // Passenger seats
            266, 268, 269, 712, 715,
            // Parents
            1093, 1253,
        };

        private static readonly int[] Antennas = {
            262, 1362, 1363, 1364, 1365, 1366,
            1877, 1878, 1879, 1880, 1881, 1882, 1883, 1884,
            // Parent
            330,
        };

        private static readonly int[] Lights = {
            279, 280, 442, 564, 569, 622, 623, 652, 653,
            1272, 1273, 1274, 1275, 1276, 1277,
            1491, 1492,
            // Spotlights
            556, 1319, 1320,
            // Parents
            1278, 1321,
        };

        private static readonly int[] Constructors = {
            // Fabricators
            264, 634, 657, 711, 713, 714, 959, 960, 961, 962,
            1446, 1447, 1628,
            // Food processor / medical / science lab
            265, 270, 271,
            // Furnaces / deconstructors
            1132, 1371,
        };

        private static readonly int[] Utilities = {
            // Life-support generators / stations
            291,  // OxygenStation
            424,  // OxygenStationSS
            554,  // OxygenGenerator
            588,  // WaterGenerator
            706,  // OxygenHydrogenGenerator
            721,  // OxygenStationSV
            964,  // OxygenGeneratorSmall
            // Repair bays + repair station + medical
            1111, 1131, 1231, 1372, 1486, 1490, 1584,
            // Medical station (parent)
            1571,
            // Trading station, ATM
            1133, 1134,
            // Clone chambers
            781, 1583,
        };

        private static readonly int[] ShieldGenerators = {
            1808, 1809, 1810, 1811, 1812, 1813, 1888,
        };

        private static readonly int[] Cores = {
            558, 560, 1360, 1361, 1401, 1402,
        };

        private static readonly int[] WarpDrives = {
            720, 1435,
        };

        private static readonly int[] CpuExtenders = {
            2023, 2024, 2025, 2026, 2027, 2028, 2029, 2030, 2031,
            2032, 2033, 2034,
        };

        private static readonly int[] Wings = {
            1139, 1140, 1141, 1150, 1151, 1152, 1153, 1154, 1155, 1156,
            1157, 1158, 1159, 1160, 1161, 1162, 1163, 1164, 1165, 1166,
            1605, 1606, 1607, 1608, 1609, 1610, 1611, 1612, 1613, 1614,
            1615, 1616, 1617, 1618, 1619, 1620, 1621, 1622, 1623, 1624,
            1625,
            // Parents
            1135, 1626,
        };

        private static readonly int[] Trusses = {
            416, 704, 705, 837, 1075,
            1406, 1407, 1408, 1409, 1410, 1411, 1412, 1413, 1414, 1415, 1416,
        };

        private static readonly int[] Npcs = {
            // Generic NPC templates
            328, 329,
            // Standing / dancing humans + aliens
            1246, 1251, 1314, 1450, 1453, 1454, 1460, 1461, 1462, 1463,
            1464, 1467, 1468, 1469, 1470, 1471, 1472, 1473, 1474, 1475,
            1476, 1477, 1519, 1520, 1643, 1644, 1645, 1646, 1647,
            // Parents
            1465, 1466,
        };

        private static readonly int[] Decorations = {
            // Furniture (basic)
            612, 613, 614, 615, 616, 617, 619, 620, 621, 624, 625, 626, 627, 628,
            629, 630, 631, 651, 612, 1493,
            // Scifi interior
            1072, 1073, 1074, 1076, 1077, 1078, 1079, 1080, 1085, 1086, 1088,
            1240, 1241, 1242, 1244, 1245, 1248, 1249, 1250, 1279, 1280,
            1338, 1398, 1399, 1452, 1456, 1458, 1459,
            // Lab / scenic props
            1282, 1283, 1284, 1285, 1286, 1287, 1288, 1289, 1290,
            1291, 1292, 1293, 1294, 1295, 1296, 1297, 1298,
            // Stone / statue decor
            1300, 1301, 1302, 1303, 1305, 1306, 1307, 1308, 1309, 1310,
            1311, 1312, 1331, 1332, 1333, 1334, 1335,
            // Misc decor
            569, 656, 654, 655, 1370, 1485, 1489, 1572, 1232,
            1736, 1737, 1738,
            // Hover engine cosmetic variants (deco, not actual thrust)
            1589, 1590,
            // SV deco (aerodynamic deco)
            1720, 1721, 1722, 1723, 1724, 1725, 1726, 1727, 1728, 1729,
            1730, 1731, 1732, 1733, 1734, 1735, 1798, 1952,
            // Tribal decor
            1740, 1741, 1742, 1743, 1744, 1745, 1746, 1747, 1748, 1749,
            1750, 1751, 1752, 1753, 1754, 1755, 1756, 1757, 1758, 1759,
            1760, 1761, 1762, 1763, 1764, 1765, 1766, 1767, 1768,
            // Talon scenic
            2072, 2073, 2074, 2075,
            // Posters
            2078, 2079, 2080, 2081, 2082, 2083, 2084, 2085, 2086, 2087,
            2088, 2089, 2090, 2091, 2092, 2093,
            // Indoor plants (decoration in pots; the pots themselves stay as full
            // structural blocks -- Unknown -- so walls/floors form around them).
            629, 630, 631, 929, 1080,
            // Ventilators (decorative-feel ducting)
            1405, 1509, 1956, 1957, 1958, 1960, 1961, 1962, 1963, 1964,
            1965, 1966,
            // Parents
            927, 1281, 1336, 1739, 1719,
        };
    }
}
