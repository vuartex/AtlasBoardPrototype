using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using UnityEngine;

public sealed class AtlasBoardFirebaseAuthFirestoreTest
    : MonoBehaviour
{
    [SerializeField]
    private bool runOnStart = true;

    [SerializeField]
    private bool cleanupAfterSuccess = true;

    private async void Start()
    {
        if (runOnStart)
        {
            await RunTest();
        }
    }

    [ContextMenu("Run Auth + Firestore Test")]
    public void RunFromContextMenu()
    {
        _ = RunTest();
    }

    private async Task RunTest()
    {
        Debug.Log(
            "[AtlasBoard Firebase Test 2] Starting Auth + Firestore test.",
            this);

        DependencyStatus dependencyStatus =
            await FirebaseApp.CheckAndFixDependenciesAsync();

        if (dependencyStatus !=
            DependencyStatus.Available)
        {
            Debug.LogError(
                "[AtlasBoard Firebase Test 2] FAILED. " +
                $"Dependencies: {dependencyStatus}",
                this);

            return;
        }

        FirebaseAuth auth =
            FirebaseAuth.DefaultInstance;

        FirebaseFirestore firestore =
            FirebaseFirestore.DefaultInstance;

        FirebaseUser createdUser = null;

        DocumentReference profileReference = null;

        string uniquePart =
            DateTime.UtcNow
                .ToString("yyyyMMddHHmmssfff");

        string testEmail =
            $"atlasboard.test.{uniquePart}@example.com";

        string testPassword =
            $"AtlasBoardTest!{uniquePart}";

        try
        {
            Debug.Log(
                "[AtlasBoard Firebase Test 2] " +
                "Creating temporary Email/Password account...",
                this);

            AuthResult authResult =
                await auth
                    .CreateUserWithEmailAndPasswordAsync(
                        testEmail,
                        testPassword);

            createdUser =
                authResult.User;

            if (createdUser == null ||
                string.IsNullOrWhiteSpace(
                    createdUser.UserId))
            {
                throw new Exception(
                    "Firebase Auth returned no valid user.");
            }

            string uid =
                createdUser.UserId;

            Debug.Log(
                "[AtlasBoard Firebase Test 2] " +
                $"Authentication PASSED. UID={uid}",
                this);

            profileReference =
                firestore
                    .Collection("users")
                    .Document(uid);

            Dictionary<string, object>
                testProfile =
                    new Dictionary<string, object>
                    {
                        {
                            "uid",
                            uid
                        },
                        {
                            "displayName",
                            "AtlasBoard Firebase Test"
                        },
                        {
                            "accountType",
                            "test"
                        },
                        {
                            "schemaVersion",
                            1
                        },
                        {
                            "createdAt",
                            FieldValue.ServerTimestamp
                        }
                    };

            Debug.Log(
                "[AtlasBoard Firebase Test 2] " +
                "Writing authenticated users/{uid} document...",
                this);

            await profileReference.SetAsync(
                testProfile);

            Debug.Log(
                "[AtlasBoard Firebase Test 2] " +
                "Firestore WRITE PASSED.",
                this);

            DocumentSnapshot snapshot =
                await profileReference.GetSnapshotAsync();

            if (!snapshot.Exists)
            {
                throw new Exception(
                    "Written Firestore document " +
                    "could not be read back.");
            }

            Dictionary<string, object> data =
                snapshot.ToDictionary();

            string readUid =
                data.TryGetValue(
                    "uid",
                    out object uidValue)
                    ? uidValue?.ToString()
                    : string.Empty;

            string readDisplayName =
                data.TryGetValue(
                    "displayName",
                    out object displayValue)
                    ? displayValue?.ToString()
                    : string.Empty;

            long schemaVersion = 0;

            if (data.TryGetValue(
                    "schemaVersion",
                    out object schemaValue) &&
                schemaValue != null)
            {
                schemaVersion =
                    Convert.ToInt64(
                        schemaValue);
            }

            if (readUid != uid)
            {
                throw new Exception(
                    "Firestore UID verification failed.");
            }

            if (readDisplayName !=
                "AtlasBoard Firebase Test")
            {
                throw new Exception(
                    "Firestore displayName " +
                    "verification failed.");
            }

            if (schemaVersion != 1)
            {
                throw new Exception(
                    "Firestore schemaVersion " +
                    "verification failed.");
            }

            Debug.Log(
                "[AtlasBoard Firebase Test 2] " +
                "Firestore READ + DATA VERIFICATION PASSED.",
                this);

            Debug.Log(
                "AtlasBoard Firebase Auth + Firestore " +
                "round-trip test PASSED. " +
                "Temporary Email/Password account was " +
                "authenticated, users/{uid} was written, " +
                "read back and verified successfully.",
                this);

            if (cleanupAfterSuccess)
            {
                Debug.Log(
                    "[AtlasBoard Firebase Test 2] " +
                    "Cleaning temporary Firestore document...",
                    this);

                await profileReference.DeleteAsync();

                Debug.Log(
                    "[AtlasBoard Firebase Test 2] " +
                    "Cleaning temporary Firebase Auth account...",
                    this);

                await createdUser.DeleteAsync();

                Debug.Log(
                    "[AtlasBoard Firebase Test 2] " +
                    "Cleanup PASSED. No temporary test " +
                    "account/data should remain.",
                    this);
            }
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[AtlasBoard Firebase Test 2] FAILED.\n" +
                exception,
                this);
        }
    }
}