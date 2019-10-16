using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AElf.Types.Tests
{
    public class BinaryMerkleTestTest
    {
        private Hash GetHashFromStrings(params string[] strings)
        {
            return BinaryMerkleTree.FromLeafNodes(strings.Select(Hash.FromString).ToList()).Root;
        }

        private Hash GetHashFromHexString(params string[] strings)
        {
            var hash = Hash.FromByteArray(ByteArrayHelper.HexStringToByteArray(strings[0]));
            foreach (var s in strings.Skip(1))
            {
                hash = Hash.FromRawBytes(hash.ToByteArray().Concat(ByteArrayHelper.HexStringToByteArray(s)).ToArray());
            }

            return hash;
        }
            
        /// <summary>
        /// Add node(s) and compute root hash
        /// </summary>
        [Fact]
        public void SingleNodeTest()
        {
            string hex = "5a7d71da020cae179a0dfe82bd3c967e1573377578f4cc87bc21f74f2556c0ef";
            var tree = BinaryMerkleTree.FromLeafNodes(new[] {CreateLeafFromHex(hex)});

            //See if the hash of merkle tree is equal to the element’s hash.
            var root = tree.Root;
            var expected = GetHashFromHexString(hex, hex);
            Assert.Equal(expected, root);
        }

        [Fact]
        public void MerkleProofTest()
        {
            string hex = "5a7d71da020cae179a0dfe82bd3c967e1573377578f4cc87bc21f74f2556c0ef";
            var leaf = CreateLeafFromHex(hex);
            var tree = BinaryMerkleTree.FromLeafNodes(new[] {leaf});

            //See if the hash of merkle tree is equal to the element’s hash.
            var root = tree.Root;
            var path = tree.GenerateMerklePath(0);
            var calculatedRoot = path.ComputeRootWithLeafNode(leaf);
            Assert.Equal(root, calculatedRoot);
        }
        
        [Fact]
        public void MerkleProofTest_MultiTwoLeaves()
        {
            string hex1 = "5a7d71da020cae179a0dfe82bd3c967e1573377578f4cc87bc21f74f2556c0ef";
            var leaf1 = CreateLeafFromHex(hex1);
            
            string hex2 = "a28bf94d0491a234d1e99abc62ed344eb55bb11aeecacc35c1b75bfa85c8983f";
            var leaf2 = CreateLeafFromHex(hex2);

            var tree = BinaryMerkleTree.FromLeafNodes(new[] {leaf1, leaf2});

            //See if the hash of merkle tree is equal to the element’s hash.
            var root = tree.Root;
            var path = tree.GenerateMerklePath(0);
            var calculatedRoot = path.ComputeRootWithLeafNode(leaf1);
            Assert.Equal(root, calculatedRoot);
        }
        
        [Fact]
        public void MerkleProofTest_MultiThreeLeaves()
        {
            string hex1 = "5a7d71da020cae179a0dfe82bd3c967e1573377578f4cc87bc21f74f2556c0ef";
            var leaf1 = CreateLeafFromHex(hex1);
            
            string hex2 = "a28bf94d0491a234d1e99abc62ed344eb55bb11aeecacc35c1b75bfa85c8983f";
            var leaf2 = CreateLeafFromHex(hex2);
            
            string hex3 = "bf6ae8809d017f07b27ad1620839c6503666fb55f7fe7ac70881e8864ce5a3ff";
            var leaf3 = CreateLeafFromHex(hex3);
            var tree = BinaryMerkleTree.FromLeafNodes(new[] {leaf1, leaf2, leaf3});

            var root = tree.Root;
            var path = tree.GenerateMerklePath(2);
            var calculatedRoot = path.ComputeRootWithLeafNode(leaf3);
            Assert.Equal(root, calculatedRoot);
        }
            
        [Fact]
        public void MerkleProofTest_MultiLeaves()
        {
            string hex1 = "5a7d71da020cae179a0dfe82bd3c967e1573377578f4cc87bc21f74f2556c0ef";
            var hash1 = CreateLeafFromHex(hex1);
            
            string hex2 = "a28bf94d0491a234d1e99abc62ed344eb55bb11aeecacc35c1b75bfa85c8983f";
            var hash2 = CreateLeafFromHex(hex2);
            
            string hex3 = "bf6ae8809d017f07b27ad1620839c6503666fb55f7fe7ac70881e8864ce5a3ff";
            var hash3 = CreateLeafFromHex(hex3);
            
            string hex4 = "bac4adcf8066921237320cdcddb721f5ba5d34065b9c54fe7f9893d8dfe52f17";
            var hash4 = CreateLeafFromHex(hex4);

            string hex5 = "bac4adcf8066921237320cdcddb721f5ba5d34065b9c54fe7f9893d8dfe52f17";
            var hash5 = CreateLeafFromHex(hex5);
            var tree = BinaryMerkleTree.FromLeafNodes(new[] {hash1, hash2, hash3, hash4, hash5});

            //See if the hash of merkle tree is equal to the element’s hash.
            var root = tree.Root;
            var path = tree.GenerateMerklePath(4);
            var calculatedRoot = path.ComputeRootWithLeafNode(hash5);
            //Assert.Contains(hash3, path.Path);
            Assert.Equal(root, calculatedRoot);
        }

        [Fact]
        public void MultiNodesTest()
        {
            var tree1 = BinaryMerkleTree.FromLeafNodes(CreateLeaves(new[] {"a", "e"}));

            //See if the hash of merkle tree is equal to the element’s hash.
            var root1 = tree1.Root;
            Assert.Equal(GetHashFromStrings("a", "e"), root1);

            var tree2 = BinaryMerkleTree.FromLeafNodes(CreateLeaves(new[] {"a", "e", "l"}));
            var root2 = tree2.Root;
            Hash right = GetHashFromStrings("l", "l");
            Assert.Equal(root1.Concat(right), root2);

            var tree3 = BinaryMerkleTree.FromLeafNodes(CreateLeaves(new[] {"a", "e", "l", "f"}));
            var root3 = tree3.Root;
            Hash right2 = GetHashFromStrings("l", "f");
            Assert.Equal(root1.Concat(right2), root3);

            var tree4 = BinaryMerkleTree.FromLeafNodes(CreateLeaves(new[] {"a", "e", "l", "f", "a"}));
            var root4 = tree4.Root;
            Hash l2 = GetHashFromStrings("a", "a");
            Hash l3 = l2.Concat(l2);
            Assert.Equal(root3.Concat(l3), root4);
        }

        [Fact]
        public void MerklePathTest1LeafNode()
        {
            var hashes = CreateLeaves(new[] {"a"});
            var tree = BinaryMerkleTree.FromLeafNodes(hashes);
            
            // test invalid index
            Assert.Throws<InvalidOperationException>(() => tree.GenerateMerklePath(1));

            // test 1st "a"
            var path = tree.GenerateMerklePath(0);
            Assert.NotNull(path);
            Assert.True(1 == path.MerklePathNodes.Count);
            var realPath = GenerateMerklePath(new[] {1}, tree.Nodes);
            Assert.Equal(realPath, path);
        }

        [Fact]
        public void MerklePathTest2LeafNodes()
        {
            var hashes = CreateLeaves(new[] {"a", "e"});
            var tree = BinaryMerkleTree.FromLeafNodes(hashes);
            // test invalid index
            Assert.Throws<InvalidOperationException>(() => tree.GenerateMerklePath(2));

            // test "a"
            var path = tree.GenerateMerklePath(0);
            Assert.NotNull(path);
            Assert.True(1 == path.MerklePathNodes.Count);
            var realPath1 = GenerateMerklePath(new[] {1}, tree.Nodes);
            Assert.Equal(realPath1, path);

            // test "e"
            path = tree.GenerateMerklePath(1);
            Assert.NotNull(path);
            Assert.True(1 == path.MerklePathNodes.Count);
            var realPath2 = GenerateMerklePath(new[] {0}, tree.Nodes);
            Assert.Equal(realPath2, path);
        }

        [Fact]
        public void MerklePathTest5LeafNodes()
        {
            var hashes = CreateLeaves(new[] {"a", "e", "l", "f", "a"});
            var tree = BinaryMerkleTree.FromLeafNodes(hashes);
            var root = tree.Root;

            // test 1st "a"
            var path1 = tree.GenerateMerklePath(0);
            Assert.NotNull(path1);
            Assert.Equal(3, path1.MerklePathNodes.Count);
            var realPath1 = GenerateMerklePath(new[] {1, 7, 11}, tree.Nodes);
            Assert.Equal(realPath1, path1);
            var actualRoot1 = ComputeRootWithMerklePathAndLeaf(Hash.FromString("a"), path1);
            Assert.Equal(root, actualRoot1);

            // test 1st "e"
            var path2 = tree.GenerateMerklePath(1);
            Assert.NotNull(path2);
            Assert.Equal(3, path2.MerklePathNodes.Count);
            var realPath2 = GenerateMerklePath(new[] {0, 7, 11}, tree.Nodes);

            Assert.Equal(realPath2, path2);
            var actualRoot2 = ComputeRootWithMerklePathAndLeaf(Hash.FromString("e"), path2);
            Assert.Equal(root, actualRoot2);

            // test 1st "l"
            var path3 = tree.GenerateMerklePath(2);
            Assert.NotNull(path3);
            Assert.Equal(3, path3.MerklePathNodes.Count);
            var realPath3 = GenerateMerklePath(new[] {3, 6, 11}, tree.Nodes);
            Assert.Equal(realPath3, path3);
            var actualRoot3 = ComputeRootWithMerklePathAndLeaf(Hash.FromString("l"), path3);
            Assert.Equal(root, actualRoot3);

            // test "f"
            var path4 = tree.GenerateMerklePath(3);
            Assert.NotNull(path4);
            Assert.Equal(3, path4.MerklePathNodes.Count);
            var realPath4 = GenerateMerklePath(new[] {2, 6, 11}, tree.Nodes);
            Assert.Equal(realPath4, path4);
            var actualRoot4 = ComputeRootWithMerklePathAndLeaf(Hash.FromString("f"), path4);
            Assert.Equal(root, actualRoot4);

            // test 2nd "a"
            var path5 = tree.GenerateMerklePath(4);
            Assert.NotNull(path5);
            Assert.Equal(3, path5.MerklePathNodes.Count);
            var realPath5 = GenerateMerklePath(new[] {5, 9, 10}, tree.Nodes);
            Assert.Equal(realPath5, path5);
            var actualRoot5 = ComputeRootWithMerklePathAndLeaf(Hash.FromString("a"), path5);
            Assert.Equal(root, actualRoot5);

            // test invalid index
            Assert.Throws<InvalidOperationException>(() => tree.GenerateMerklePath(5));
        }

        [Theory]
        [InlineData(16, 0)]
        [InlineData(16, 15)]
        [InlineData(9, 8)]
        public void MerklePathTest(int leafCount, int index)
        {
            var hashes = CreateLeaves(leafCount);
            var tree = BinaryMerkleTree.FromLeafNodes(hashes.ToArray());
            var root = tree.Root;
            var path = tree.GenerateMerklePath(index);
            var calculatedRoot = path.ComputeRootWithLeafNode(hashes[index]);
            Assert.Equal(root, calculatedRoot);
        }

        [Theory]
        [InlineData(4, 7)]
        [InlineData(5, 13)]
        [InlineData(6, 13)]
        [InlineData(7, 15)]
        [InlineData(9, 23)]
        public void MerkleNodesCountTest(int leafCount, int expectCount)
        {
            var hashes = CreateLeaves(leafCount);
            var tree = BinaryMerkleTree.FromLeafNodes(hashes);
            var nodesCount = tree.Nodes.Count;
            Assert.Equal(expectCount, nodesCount);
        }

        #region Some useful methods
        
        private List<Hash> CreateLeaves(IEnumerable<string> buffers)
        {
            return buffers.Select(Hash.FromString).ToList();
        }

        private List<Hash> CreateLeaves(int i)
        {
            List<Hash> res = new List<Hash>();
            for (int j = 0; j < i; j++)
            {
                res.Add(Hash.FromString(j.ToString()));
            }

            return res;
        }

        private Hash CreateLeafFromHex(string hex)
        {
            return Hash.FromByteArray(ByteArrayHelper.HexStringToByteArray(hex));
        }

        private Hash ComputeRootWithMerklePathAndLeaf(Hash leaf, MerklePath path)
        {
            return path.ComputeRootWithLeafNode(leaf);
        }

        private MerklePath GenerateMerklePath(IList<int> index, IList<Hash> hashes)
        {
            var merklePath = new MerklePath();
            foreach (var i in index)
            {
                merklePath.MerklePathNodes.Add(new MerklePathNode
                {
                    Hash = hashes[i],
                    IsLeftChildNode = i % 2 == 0
                });
            }

            return merklePath;
        }
        
        #endregion

//        [Fact]
//        public void Test()
//        {
//            string[] strings = 
//            {
//                "47f1db99b27269d9006cee06e366a9f799f86193e48e341cf7c64f450d867718",
//                "3defa5818d89b23f69907bebe4ec85bd78f9dc672aa77b2bc69f893890619b53",
//                "8d9aeec90b5a361a38eef2dd7c316edc210afd9ab0f4b0103ae7f7f038ff63cd",
//                "68d7033c8bd66639067a7737761a137dfa0296cc7e6362bd0ac2b0a20d6f3e59",
//                "215f915186c7ffe02416cc06972d2c99501db7abac9d71e3c3fe7ec56e7802c7",
//                "dba39c79fe8a7293f9b3b3fb0e0091c9f9c04b086dd22a44b40d6f9cedccf82f",
//                "8040ba8b4592a033c4943de22ea839d3fe998fe0a810f233dbd7792c2a77686f",
//                "e9425d8f9114867c70bf2d4707d493e8fde328797cb5ae980d1815f95f5323ee",
//                "a14984f5313e50924dc5f50713713b1da716a3f1ebf5da59b80cebf397f70502",
//                "4ca0583bae669a7b93ec01fd842d79af535c974caa8d4e23bf210bddc3695299",
//                "706ceb7e69ca4c00410db8a3677e157e738a17b781715c056f0ddca8a4b6b45b",
//                "0df7ec936c807f3173a1db2e948ba26f1b87cc2535ab271b8d3443aeaf88f048",
//                "1735e0d2b76343d56048c17badc143d9ed0a5fb52089f28fde4bbf034e9f353e",
//                "e45f780242ea54108c994341dc8d245c0fbcee18aa552f79688b2858350252fe",
//                "e0ea687fdfdbb699faaf9be9ffe8f9b66047aae128fdc9ab448fd5519dc911a2"
//            };
//
//            var hashList = strings.Select(HashHelper.HexStringToHash).ToList();
//            var root = BinaryMerkleTree.FromLeafNodes(hashList).Root;
//            var toHex = root.ToHex();
//            Assert.Equal("633321293076f5f2bf898c1fb41bcaa438cbf8949311dd60fb890c43f8b7d357", toHex);
//        }
    }
}