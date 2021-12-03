import numpy as np
import torch
import pandas
import matplotlib.pyplot as plt
import time

class NeuralNetwork(torch.nn.Module):
    
    def __init__(self, n_inputs, n_hiddens_per_layer, n_outputs, act_func_per_layer, device='cpu'):
        super().__init__()  # call parent class (torch.nn.Module) constructor
        
        # Set self.n_hiddens_per_layer to [] if argument is 0, [], or [0]
        if n_hiddens_per_layer == 0 or n_hiddens_per_layer == [] or n_hiddens_per_layer == [0]:
            self.n_hiddens_per_layer = []
        else:
            self.n_hiddens_per_layer = n_hiddens_per_layer

        self.hidden_layers = torch.nn.ModuleList()  # necessary for model.to('cuda')

        for i in range(len(self.n_hiddens_per_layer)):
            nh = self.n_hiddens_per_layer[i]
            act_func = torch.nn.Tanh()
            if act_func_per_layer[i].lower() == 'tanh':
                act_func = torch.nn.Tanh()
            elif act_func_per_layer[i].lower() == 'relu':
                act_func = torch.nn.ReLU()
            elif act_func_per_layer[i].lower() == 'prelu':
                act_func = torch.nn.PReLU()
            elif act_func_per_layer[i].lower() == 'rrelu':
                act_func = torch.nn.RReLU()
            elif act_func_per_layer[i].lower() == 'leakyrelu':
                act_func = torch.nn.LeakyReLU()
            elif act_func_per_layer[i].lower() == 'elu':
                act_func = torch.nn.ELU()
            elif act_func_per_layer[i].lower() == 'silu':
                act_func = torch.nn.SiLU()
            elif act_func_per_layer[i].lower() == 'gelu':
                act_func = torch.nn.GELU()
            elif act_func_per_layer[i].lower() == 'celu':
                act_func = torch.nn.CELU()
            else:
                act_func = torch.nn.Tanh()
            self.hidden_layers.append(
                torch.nn.Sequential(
                    torch.nn.Linear(n_inputs, nh), act_func)
                )
            
            n_inputs = nh

        self.output_layer = torch.nn.Linear(n_inputs, n_outputs)
            
        self.standardize = ''
        self.Xmeans = None
        self.Xstds = None
        self.Tmeans = None
        self.Tstds = None

        self.error_trace = []
        
        self.device = device
        self.to(self.device)
        
    def forward_to_middle_layer(self, X):
        middle_layer = len(self.hidden_layers) // 2
        Y = X
        for hidden_layer in self.hidden_layers[:middle_layer + 1]:
            Y = hidden_layer(Y)
        return Y

    def forward(self, X):
        Y = X
        for hidden_layer in self.hidden_layers:
            Y = hidden_layer(Y)
        Y = self.output_layer(Y)
        return Y

    def fit(self, X, T, n_epochs, learning_rate, method='adam', verbose=True, standardize='X T'):

        self.standardize = standardize
        
        # Set data matrices to torch.tensors if not already.
        if not isinstance(X, torch.Tensor):
            X = torch.from_numpy(X).float().to(self.device)
        if not isinstance(T, torch.Tensor):
            T = torch.from_numpy(T).float().to(self.device)
            
        # Calculate standardization parameters if not already calculated
        if 'X' in self.standardize and self.Xmeans is None:
            self.Xmeans = X.mean(0)
            self.Xstds = X.std(0)
            self.Xstds[self.Xstds == 0] = 1

        if 'T' in self.standardize and self.Tmeans is None:
            self.Tmeans = T.mean(0)
            self.Tstds = T.std(0)
            self.Tstds[self.Tstds == 0] = 1
            
        # Standardize inputs and targets
        if 'X' in self.standardize:
            X = (X - self.Xmeans) / self.Xstds
        if 'T' in self.standardize:
            T = (T - self.Tmeans) / self.Tstds
        
        # Set optimizer to Adam and loss functions to MSELoss
        if method == 'adam':
            optimizer = torch.optim.Adam(self.parameters(), lr=learning_rate)
        elif method == 'sgd':
            optimizer = torch.optim.SGD(self.parameters(), lr=learning_rate, momentum=0.5)
        mse_func = torch.nn.MSELoss()

        # For each epoch:
        #   Do forward pass to calculate output Y.
        #   Calculate mean squared error loss, mse.
        #   Calculate gradient of mse with respect to all weights by calling mse.backward().
        #   Take weight update step, then zero the gradient values.
        #   Unstandardize the mse error and save in self.error_trace
        #   Print epoch+1 and unstandardized error if verbose is True and
        #             (epoch+1 is n_epochs or epoch+1 % (n_epochs // 10) == 0)
        
        for epoch in range(n_epochs):

            Y = self(X)
            
            mse = mse_func(T, Y)
            mse.backward()
            
            optimizer.step() 
            optimizer.zero_grad()

            err = torch.sqrt(mse)  # * self.Tstds
            self.error_trace.append(err.item())  #.detach().cpu())

            if verbose and ((epoch + 1) == n_epochs or (epoch + 1) % max(1, (n_epochs // 10)) == 0):
                print(f'Epoch {epoch+1}: RMSE {err.item():.3f}')

    def use(self, X):
 
        # Set input matrix to torch.tensors if not already.
        if not isinstance(X, torch.Tensor):
            X = torch.from_numpy(X).float().to(self.device)

        # Standardize X

        if 'X' in self.standardize:
            X = (X - self.Xmeans) / self.Xstds
        # Do forward pass and unstandardize resulting output
        Y = self(X)
        if 'T' in self.standardize:
            Y = Y * self.Tstds + self.Tmeans

        # Return output Y after detaching from computation graph and converting to numpy
        return Y.detach().cpu().numpy()

    def use_to_middle(self, X):
 
        # Set input matrix to torch.tensors if not already.
        if not isinstance(X, torch.Tensor):
            X = torch.from_numpy(X).float().to(self.device)

        # Standardize X

        if 'X' in self.standardize:
            X = (X - self.Xmeans) / self.Xstds

        # Do forward pass and unstandardize resulting output
        Y = self.forward_to_middle_layer(X)

        # Return output Y after detaching from computation graph and converting to numpy
        return Y.detach().cpu().numpy()
    

######################################################################

class NeuralNetwork_Classifier(NeuralNetwork):
    
    def train(self, X, T, n_epochs, learning_rate, method='adam', verbose=True, standardize='X'):

        # T must be long ints
        # self.classes = np.unique(T)
        
        self.standardize = standardize
        
        # Set data matrices to torch.tensors if not already.
        if not isinstance(X, torch.Tensor):
            X = torch.from_numpy(X).float().to(self.device)
        if not isinstance(T, torch.Tensor):
            T = torch.from_numpy(T).long().to(self.device)
            
        # Calculate standardization parameters if not already calculated
        if 'X' in self.standardize and self.Xmeans is None:
            self.Xmeans = X.mean(0)
            self.Xstds = X.std(0)
            self.Xstds[self.Xstds == 0] = 1

        # if 'T' in self.standardize and self.Tmeans is None:
        #     self.Tmeans = T.mean(0)
        #     self.Tstds = T.std(0)
        #     self.Tstds[self.Tstds == 0] = 1
            
        # Standardize inputs and targets
        if 'X' in self.standardize:
            X = (X - self.Xmeans) / self.Xstds
        # if 'T' in self.standardize:
        #     T = (T - self.Tmeans) / self.Tstds
        
        # Set optimizer to Adam and loss functions to MSELoss
        if method == 'adam':
            optimizer = torch.optim.Adam(self.parameters(), lr=learning_rate)
        elif method == 'sgd':
            optimizer = torch.optim.SGD(self.parameters(), lr=learning_rate, momentum=0.5)

        CE_func = torch.nn.CrossEntropyLoss(reduction='mean')
        
        for epoch in range(n_epochs):

            Y = self(X)
                        
            ce = CE_func(Y, T)
            ce.backward()
            
            optimizer.step() 
            optimizer.zero_grad()

            err = ce
            self.error_trace.append(err.item())  #.detach().cpu())

            if verbose and ((epoch + 1) == n_epochs or (epoch + 1) % (n_epochs // 10) == 0):
                print(f'Epoch {epoch+1}: RMSE {err.item():.3f}')

        return self

    def softmax(self, Y):
        '''Apply to final layer weighted sum outputs'''
        # Trick to avoid overflow
        maxY = torch.max(Y, axis=1)[0].reshape((-1,1))
        expY = torch.exp(Y - maxY)
        denom = torch.sum(expY, axis=1).reshape((-1, 1))
        Y = expY / denom
        return Y

    def use(self, X):
        # Set input matrix to torch.tensors if not already.
        if not isinstance(X, torch.Tensor):
            X = torch.from_numpy(X).float().to(self.device)

        Y = self.forward(X)
        probs = self.softmax(Y)  # .detach().cpu().numpy()
        classes = torch.argmax(probs, axis=1)  # .detach().cpu().numpy()
        return (classes.detach().cpu().numpy(),
                probs.detach().cpu().numpy())

