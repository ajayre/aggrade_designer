using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Core.Tests.Models
{
    /// <summary>
    /// Unit tests for <see cref="TopoGeometryDirtyState"/>.
    /// </summary>
    public sealed class TopoGeometryDirtyStateTests
    {
        /// <summary>
        /// Cut/fill geometry is stale when either existing or proposed topo is dirty.
        /// </summary>
        [Fact]
        public void IsDirtyForContentView_CutFill_IsStaleWhenEitherTopoDirty
            (
            )
        {
            TopoGeometryDirtyState Dirty = new TopoGeometryDirtyState
            {
                ExistingIsDirty = false,
                ProposedIsDirty = true
            };

            Assert.True(Dirty.IsDirtyForContentView(FieldContentView.CutFill));
        }

        /// <summary>
        /// Building cut/fill geometry clears both topo dirty flags.
        /// </summary>
        [Fact]
        public void ClearAfterBuild_CutFill_ClearsBothFlags
            (
            )
        {
            TopoGeometryDirtyState Dirty = new TopoGeometryDirtyState();
            Dirty.ClearAfterBuild(FieldContentView.CutFill);

            Assert.False(Dirty.ExistingIsDirty);
            Assert.False(Dirty.ProposedIsDirty);
        }
    }
}
