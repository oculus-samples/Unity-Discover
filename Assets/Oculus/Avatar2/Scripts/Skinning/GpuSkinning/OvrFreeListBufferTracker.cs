using System.Collections.Generic;

namespace Oculus.Skinning.GpuSkinning
{
    public class OvrFreeListBufferTracker
    {
        public struct LayoutResult
        {
            public static readonly LayoutResult Invalid = new LayoutResult(int.MaxValue, 0);
            public LayoutResult(int startIdx, int numAtIdx)
            {
                startIndex = startIdx;
                count = numAtIdx;
            }

            public bool IsValid => startIndex < int.MaxValue;

            public readonly int startIndex;
            public readonly int count;
        };

        public OvrFreeListBufferTracker(int maxSize)
        {
            _nodes = new LinkedList<TrackerNode>();
            _freeNodes = new List<LinkedListNode<TrackerNode>>();
            _handleGenerator = new OvrHandleGenerator();
            _handleToNode = new Dictionary<OvrSkinningTypes.Handle, LinkedListNode<TrackerNode>>();

            _sizeNeeded = 0;
            _maxSize = maxSize;
        }

        public OvrSkinningTypes.Handle TrackBlock(int numInBlock)
        {
            OvrSkinningTypes.Handle handle = _handleGenerator.GetHandle();

            // Look to see if there are free nodes that fit
            var listNodeThatCanFit = FindFreeNodeThatCanFit(numInBlock);
            if (listNodeThatCanFit != null)
            {
                var nodeThatCanFit = listNodeThatCanFit.Value;

                // Remove from free nodes and update underlying node
                _freeNodes.Remove(listNodeThatCanFit);

                // See if node needs to be "split" into another node
                // to be put onto the free list
                if (nodeThatCanFit.size != numInBlock)
                {
                    // Create new node
                    // New "free node" has reduced size from previously free node
                    var newNode = new TrackerNode
                    {
                        startIndex = nodeThatCanFit.startIndex + numInBlock,
                        size = nodeThatCanFit.size - numInBlock,
                        isFree = true,
                    };

                    // Insert into nodes linked list and into free nodes list
                    var addedNode = _nodes.AddAfter(listNodeThatCanFit, newNode);
                    _freeNodes.Add(addedNode);

                    // Sort free nodes
                    _freeNodes.Sort(sComparer);
                }

                // Update no longer free node to be of new size
                nodeThatCanFit.isFree = false;
                nodeThatCanFit.size = numInBlock;

                _handleToNode[handle] = listNodeThatCanFit;
                return handle;
            } // end if found a free node that can fit

            // No existing node fits, make new node
            int newNodesStartIndex = 0;
            if (_nodes.Count != 0)
            {
                var lastNode = _nodes.Last.Value;
                newNodesStartIndex = lastNode.startIndex + lastNode.size;
            }

            var newListNode = _nodes.AddLast(new TrackerNode
            {
                startIndex = newNodesStartIndex,
                isFree = false,
                size = numInBlock,
            });

            _sizeNeeded += numInBlock;

            // Add to mapping
            _handleToNode[handle] = newListNode;
            return handle;
        }

        public LayoutResult GetLayoutInBufferForBlock(OvrSkinningTypes.Handle handle)
        {
            return _handleToNode.TryGetValue(handle, out LinkedListNode<TrackerNode> listNode) ?
                new LayoutResult(listNode.Value.startIndex, listNode.Value.size) :
                LayoutResult.Invalid;
        }

        public void FreeBlock(OvrSkinningTypes.Handle handle)
        {
            // See if even found
            if (!_handleToNode.TryGetValue(handle, out LinkedListNode<TrackerNode> listNode))
            {
                return;
            }

            // Remove node from mapping and add to free nodes, but also
            // see if it any of the node's previous or next node (or both) can be joined
            // together to form a larger free node

            // Remove from mapping
            TrackerNode nodeToFree = listNode.Value;
            _handleToNode.Remove(handle);

            // Don't actually remove the node, just move it to free list
            nodeToFree.isFree = true;

            bool keepNode = true;
            // See if "next node" is free, if so, "absorb" into this one
            LinkedListNode<TrackerNode> otherListNode = listNode.Next;
            if (otherListNode != null)
            {
                TrackerNode otherNode = otherListNode.Value;
                if (otherNode.isFree)
                {
                    // Absorb the other node into "node to free"
                    nodeToFree.size += otherNode.size;

                    // Remove "other node" from free nodes
                    // and nodes list (removes node)
                    _freeNodes.Remove(otherListNode);
                    _nodes.Remove(otherListNode);
                }
            }

            // See if "previous node" is free, if so, "absorb" node into the previous one
            otherListNode = listNode.Previous;
            if (otherListNode != null)
            {
                TrackerNode otherNode = otherListNode.Value;
                if (otherNode.isFree)
                {
                    otherNode.size += nodeToFree.size;
                    keepNode = false;
                }
            }

            if (!keepNode)
            {
                // Remove node from _nodes, do not add to free nodes list
                _nodes.Remove(listNode);
            }
            else
            {
                _freeNodes.Add(listNode);

                // Sort free nodes
                _freeNodes.Sort(sComparer);
            }
        }

        public int BufferSizeNeeded()
        {
            return _sizeNeeded;
        }

        public bool CanFit(int size)
        {
            if (CanFitInMaximumSize(size))
            {
                return true;
            }

            LinkedListNode<TrackerNode> listNodeThatCanFit = FindFreeNodeThatCanFit(size);
            return listNodeThatCanFit != null;
        }

        private bool CanFitInMaximumSize(int size)
        {
            return BufferSizeNeeded() + size < _maxSize;
        }

        private LinkedListNode<TrackerNode> FindFreeNodeThatCanFit(int size)
        {
            // See if any of the free nodes can fit by using binary search
            if (_freeNodes.Count == 0)
            {
                return null;
            }

            // See if any of the free nodes can fit by using binary search
            LinkedListNode<TrackerNode> comparisonNode = new LinkedListNode<TrackerNode>(new TrackerNode
            {
                size = size,
            });
            int nodeThatCanFitIndex = _freeNodes.BinarySearch(comparisonNode, sComparer);

            // See if a node that can fit exact number was found
            if (nodeThatCanFitIndex < 0)
            {
                // Binary search returns bitwise compliment of index that can fit
                // or the bitwise compliment of Count
                nodeThatCanFitIndex = ~nodeThatCanFitIndex;
            }

            return nodeThatCanFitIndex != _freeNodes.Count ? _freeNodes[nodeThatCanFitIndex] : null;
        }

        private struct TrackerNode
        {
            public int size;
            public int startIndex;
            public bool isFree;
        };

        private class TrackerNodeComparer : IComparer<LinkedListNode<TrackerNode>>
        {
            public int Compare(LinkedListNode<TrackerNode> x, LinkedListNode<TrackerNode> y)
            {
                if (x.Value.size > y.Value.size)
                    return 1;
                if (x.Value.size < y.Value.size)
                    return -1;
                return 0;

            }
        }

        private readonly LinkedList<TrackerNode> _nodes;
        private readonly List<LinkedListNode<TrackerNode>> _freeNodes;
        private readonly OvrHandleGenerator _handleGenerator;
        private readonly Dictionary<OvrSkinningTypes.Handle, LinkedListNode<TrackerNode>> _handleToNode;

        private static readonly TrackerNodeComparer sComparer = new TrackerNodeComparer();

        private int _sizeNeeded;
        private readonly int _maxSize;
    }
}
