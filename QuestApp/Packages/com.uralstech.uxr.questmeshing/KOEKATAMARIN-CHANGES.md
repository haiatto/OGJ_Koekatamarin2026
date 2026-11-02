# Embedded UXR.QuestMeshing 1.5.1

Upstream: https://github.com/Uralstech/UXR.QuestMeshing/tree/1.5.1

The release tag is the UPM package layout, not the master Unity project layout.
Embedded in Koekatamarin 2026 on 2026-11-03. Apache-2.0 and third-party notices
are included here and in the APK's `assets/Notices` directory.
Utils.Singleton 2.0.0 is embedded separately; its OpenUPM archive was verified
against the registry SHA-512 integrity value.

Local modifications (haiatto / Koekatamarin 2026):

- DepthMesher: generation counter rejects in-flight GPU readbacks and collider
  publication after clear/disable; Clear immediately detaches the collider and
  emits OnCleared without modifying mesh buffers used by an outstanding bake.
- Empty extraction removes the previous published geometry instead of returning
  with a stale collider. Added IsInitialized for lifecycle coordination.
- Recenter event registration tolerates a missing OVR display for Editor checks.
- SurfaceNets: bound the final partial frustum dispatch group and discard voxels
  outside the volume rather than clamping observations onto its boundary.
- DepthPreprocessor: restart the occlusion subsystem after disable/re-enable;
  retry unavailable startup descriptors; reuse preprocessing render textures;
  enable supported hand removal; correctly disable global occlusion keywords;
  release UnityXRDisplay texture registrations on suspension.

The TSDF update and Surface Nets algorithm remain upstream's implementation.
App-specific voxel/range/update/budget settings live in ProjectSetup.cs.
NavMesh generation is disabled because words use Rigidbody/MeshCollider.
Any future upstream update must re-evaluate these changes and device behavior.
