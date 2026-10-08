using CodeWalker.GameFiles;

namespace RageLightEditor.Editor
{
    public partial class ProjectWindow
    {
        public void SetEntityArchetype_U31(YmapEntityDef ent, uint hash)
        {
            if (ent == null) return;
            var name = new MetaHash(hash);
            var def = ent.MloParent?.MloInstance?.TryGetArchetypeEntity(ent);
            ent._CEntityDef.archetypeName = name;
            if (def != null) def._Data.archetypeName = name;
            var arch = Project?.FindArchetype(name, ArchetypeCache) ?? ArchetypeCache?.GetArchetype(hash);
            if (ReferenceEquals(ent.Archetype, arch)) return;
            ent.SetArchetype(arch);
            if (ent.IsMlo && ent.MloInstance != null && ArchetypeCache != null) ent.MloInstance.InitYmapEntityArchetypes(ArchetypeCache);
        }

        public YmapEntityDef SetMloDefArchetype_U31(MCEntityDef me, uint hash)
        {
            if (me == null) return null;
            var inst = FindMloInstance?.Invoke(me.OwnerMlo);
            var live = inst?.TryGetYmapEntity(me) ?? LiveEntityByIndex(inst, me);
            me._Data.archetypeName = new MetaHash(hash);
            if (live != null) SetEntityArchetype_U31(live, hash);
            return live;
        }
    }
}
