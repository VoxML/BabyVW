from typing import Any, Dict, List, Optional, Type, Union

import gym
import torch as th
from torch import nn

from stable_baselines3.td3.policies import TD3Policy

class TestPolicy(TD3Policy):
    def forward(self, observation: th.Tensor, deterministic: bool = False) -> th.Tensor:
        return self._predict(observation, deterministic=deterministic) * 0.

    def _predict(self, observation: th.Tensor, deterministic: bool = False) -> th.Tensor:
          # Note: the deterministic parameter is ignored in the case of TD3.
          #   Predictions are always deterministic
        return self.actor(observation) * 0.
