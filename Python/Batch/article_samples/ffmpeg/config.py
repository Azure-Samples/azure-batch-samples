# -------------------------------------------------------------------------
# Global constant variables (Azure Storage account/Batch details)
#
# Imported by "batch_python_tutorial_ffmpeg.py".
#
# Update the Batch and Storage account values below with the names unique to
# your accounts. Authentication uses Microsoft Entra ID through
# DefaultAzureCredential, so no account keys are required. Sign in with
# `az login` and make sure your identity has the required roles on both
# accounts (see the README).
# -------------------------------------------------------------------------

_BATCH_ACCOUNT_NAME = ''
_BATCH_ACCOUNT_URL = ''
_STORAGE_ACCOUNT_NAME = ''
_POOL_ID = 'LinuxFfmpegPool'
_DEDICATED_POOL_NODE_COUNT = 0
_LOW_PRIORITY_POOL_NODE_COUNT = 5
_POOL_VM_SIZE = 'STANDARD_A1_v2'
_JOB_ID = 'LinuxFfmpegJob'
